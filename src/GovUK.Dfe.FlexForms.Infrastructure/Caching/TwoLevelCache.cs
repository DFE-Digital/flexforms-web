using System.Collections.Concurrent;
using System.Text.Json;
using GovUK.Dfe.CoreLibs.Caching.Settings;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace GovUK.Dfe.FlexForms.Infrastructure.Caching;

/// <summary>
/// Reads from this replica's memory first, then the shared Redis copy (<see cref="ISharedCacheStore"/>), then the
/// source. <see cref="RemoveAsync"/> invalidates the Redis copy and broadcasts the key so every replica drops its
/// memory copy at once; a replica that misses the broadcast (pub/sub does not queue) keeps its copy for at most the
/// short memory duration. If Redis is unavailable, reads go to the source and the app still starts. If invalidating
/// the Redis copy fails, the key is retried in the background and bypasses Redis on this replica until it succeeds,
/// so a save made during a Redis blip cannot be undone by the old shared copy once Redis recovers.
/// </summary>
public sealed class TwoLevelCache(
    IMemoryCache memoryCache,
    IOptions<CacheSettings> cacheSettings,
    IServiceProvider serviceProvider,
    ILogger<TwoLevelCache> logger)
    : ReplicaBroadcastService(Channel, serviceProvider, logger), ITwoLevelCache
{
    internal static readonly RedisChannel Channel = RedisChannel.Literal(FlexFormsCacheKeys.LocalCacheKeyRemovedChannel);

    // Same shape as the CoreLibs Redis cache, so FormTemplate's round trip is covered by the same expectations.
    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private const string MemoryKeyPrefix = "TwoLevelCache:";

    private readonly ConcurrentDictionary<string, long> _generations = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _fills = new();
    // Key -> number of the latest failed invalidation attempt for it. Attempt numbers only grow.
    private readonly ConcurrentDictionary<string, long> _pendingInvalidations = new();
    private readonly Lock _sync = new();
    private long _invalidationAttempts;

    public TimeSpan InvalidationRetryInterval { get; init; } = TimeSpan.FromSeconds(5);

    public async Task<T> GetOrAddAsync<T>(
        string cacheKey,
        Func<Task<T>> fetchFunction,
        string methodName,
        CancellationToken cancellationToken = default)
    {
        if (TryGetFromMemory(cacheKey, out T cached))
        {
            return cached;
        }

        // One fill per key on this replica; the others wait and then read memory.
        var fill = _fills.GetOrAdd(cacheKey, static _ => new SemaphoreSlim(1, 1));
        await fill.WaitAsync(cancellationToken);
        try
        {
            if (TryGetFromMemory(cacheKey, out cached))
            {
                return cached;
            }

            var generation = _generations.GetValueOrDefault(cacheKey);
            var value = await LoadAsync(cacheKey, fetchFunction, methodName);

            // A load that overlapped a removal may have produced the old value; keep it out of memory.
            lock (_sync)
            {
                if (value is not null && _generations.GetValueOrDefault(cacheKey) == generation)
                {
                    var memory = cacheSettings.Value.Memory;
                    memoryCache.Set(
                        MemoryKeyPrefix + cacheKey,
                        value,
                        Duration(memory?.Durations, memory?.DefaultDurationInSeconds, methodName));
                }
            }

            return value;
        }
        finally
        {
            fill.Release();
        }
    }

    public async Task RemoveAsync(string cacheKey)
    {
        // Redis first: a local read that starts after the memory copy is dropped must not find the old shared copy.
        if (!await InvalidateSharedAsync(cacheKey))
        {
            Logger.LogWarning(
                "Could not remove {CacheKey} from Redis; retrying every {RetryInterval} and bypassing the shared copy until then",
                cacheKey,
                InvalidationRetryInterval);
        }

        RemoveLocal(cacheKey);
        await PublishAsync(cacheKey);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(base.ExecuteAsync(stoppingToken), RetryPendingInvalidationsAsync(stoppingToken));

    /// <summary>
    /// Retries every invalidation that failed. On success the other replicas are told again, because they may have
    /// re-read the old shared copy after Redis recovered.
    /// </summary>
    internal async Task RetryPendingInvalidationsOnceAsync()
    {
        foreach (var cacheKey in _pendingInvalidations.Keys)
        {
            if (!await InvalidateSharedAsync(cacheKey))
            {
                continue;
            }

            RemoveLocal(cacheKey);
            await PublishAsync(cacheKey);
            Logger.LogInformation("Removed {CacheKey} from Redis after an earlier failure", cacheKey);
        }
    }

    private async Task RetryPendingInvalidationsAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(InvalidationRetryInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RetryPendingInvalidationsOnceAsync();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
    }

    /// <summary>
    /// Invalidates the Redis copy and keeps the pending marker in step. A success only clears a marker left by an
    /// attempt that started no later than this one: a later save's failed attempt may have run while this one's reply
    /// was in flight, after a refill of the entry this attempt deleted, and that save still needs retrying.
    /// </summary>
    private async Task<bool> InvalidateSharedAsync(string cacheKey)
    {
        var attempt = Interlocked.Increment(ref _invalidationAttempts);
        if (!await TryInvalidateSharedAsync(cacheKey))
        {
            _pendingInvalidations.AddOrUpdate(cacheKey, attempt, (_, latest) => Math.Max(latest, attempt));
            return false;
        }

        while (_pendingInvalidations.TryGetValue(cacheKey, out var latestFailure) && latestFailure <= attempt)
        {
            if (_pendingInvalidations.TryRemove(KeyValuePair.Create(cacheKey, latestFailure)))
            {
                break;
            }
        }

        return true;
    }

    private async Task<bool> TryInvalidateSharedAsync(string cacheKey)
    {
        if (TryGetStore() is not { } store)
        {
            return false;
        }

        try
        {
            await store.InvalidateAsync(cacheKey);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not remove {CacheKey} from Redis", cacheKey);
            return false;
        }
    }

    public async Task<bool> ContainsAsync(string cacheKey)
    {
        var store = TryGetStore() ?? throw new InvalidOperationException("Redis is unavailable.");
        return (await store.ReadAsync(cacheKey)).Value is not null;
    }

    protected override void OnRemoteMessage(string payload)
    {
        RemoveLocal(payload);
        Logger.LogInformation("Dropped in-memory cache key {CacheKey} after a change on another Web replica", payload);
    }

    private async Task<T> LoadAsync<T>(string cacheKey, Func<Task<T>> fetchFunction, string methodName)
    {
        // A key whose Redis invalidation has not gone through yet may still have the old copy there.
        var store = _pendingInvalidations.ContainsKey(cacheKey) ? null : TryGetStore();
        SharedCacheEntry? shared = null;
        if (store is not null)
        {
            try
            {
                shared = await store.ReadAsync(cacheKey);
                if (shared.Value.Value is { } bytes && JsonSerializer.Deserialize<T>(bytes, SerializerOptions) is { } stored)
                {
                    return stored;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Could not read {CacheKey} from Redis; loading from source", cacheKey);
                store = null;
            }
        }

        var value = await fetchFunction();

        if (store is not null && shared is { } entry && value is not null)
        {
            try
            {
                var redisSettings = cacheSettings.Value.Redis;
                var written = await store.WriteIfUnchangedAsync(
                    cacheKey,
                    JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions),
                    entry.Version,
                    Duration(redisSettings?.Durations, redisSettings?.DefaultDurationInSeconds, methodName));
                if (!written)
                {
                    Logger.LogInformation("Not caching {CacheKey}: it was invalidated while loading", cacheKey);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Could not write {CacheKey} to Redis", cacheKey);
            }
        }

        return value;
    }

    private bool TryGetFromMemory<T>(string cacheKey, out T value)
    {
        if (memoryCache.TryGetValue(MemoryKeyPrefix + cacheKey, out T? cached) && cached is not null)
        {
            value = cached;
            return true;
        }

        value = default!;
        return false;
    }

    private void RemoveLocal(string cacheKey)
    {
        lock (_sync)
        {
            _generations.AddOrUpdate(cacheKey, 1, static (_, generation) => generation + 1);
            memoryCache.Remove(MemoryKeyPrefix + cacheKey);
        }
    }

    // Resolved per use: the Redis connection is created on first resolve and throws while Redis is unreachable.
    private ISharedCacheStore? TryGetStore()
    {
        try
        {
            return Services.GetRequiredService<ISharedCacheStore>();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Redis cache unavailable; using memory and the source only");
            return null;
        }
    }

    private static TimeSpan Duration(Dictionary<string, int>? durations, int? defaultSeconds, string methodName) =>
        TimeSpan.FromSeconds(durations is not null && durations.TryGetValue(methodName, out var seconds)
            ? seconds
            : defaultSeconds ?? 60);
}
