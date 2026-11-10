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
/// short memory duration. If Redis is unavailable, reads go to the source and the app still starts.
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
    private readonly Lock _sync = new();

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
        if (TryGetStore() is { } store)
        {
            try
            {
                await store.InvalidateAsync(cacheKey);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Could not remove {CacheKey} from Redis; it stays until it expires", cacheKey);
            }
        }

        RemoveLocal(cacheKey);
        await PublishAsync(cacheKey);
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
        var store = TryGetStore();
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
