using System.Collections.Concurrent;
using GovUK.Dfe.CoreLibs.Caching.Interfaces;
using GovUK.Dfe.CoreLibs.Caching.Settings;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace GovUK.Dfe.FlexForms.Web.Services;

/// <summary>
/// Reads from this replica's memory first, then Redis, then the source. <see cref="RemoveAsync"/> deletes the Redis copy
/// and broadcasts the key so every replica drops its in-memory copy at once. A replica that misses the broadcast
/// (pub/sub does not queue) serves its copy until the short memory duration ends.
/// </summary>
public sealed class TwoLevelCache(
    IMemoryCache memoryCache,
    IAdvancedRedisCacheService redisCache,
    IOptions<CacheSettings> cacheSettings,
    IServiceProvider serviceProvider,
    ILogger<TwoLevelCache> logger)
    : ReplicaBroadcastService(Channel, serviceProvider, logger), ITwoLevelCache
{
    internal static readonly RedisChannel Channel = RedisChannel.Literal(FlexFormsCacheKeys.LocalCacheKeyRemovedChannel);

    private const string MemoryKeyPrefix = "TwoLevelCache:";

    private readonly ConcurrentDictionary<string, long> _generations = new();
    private readonly Lock _sync = new();

    public async Task<T> GetOrAddAsync<T>(
        string cacheKey,
        Func<Task<T>> fetchFunction,
        string methodName,
        CancellationToken cancellationToken = default)
    {
        if (memoryCache.TryGetValue(MemoryKeyPrefix + cacheKey, out T? cached) && cached is not null)
        {
            return cached;
        }

        var generation = _generations.GetValueOrDefault(cacheKey);
        var value = await redisCache.GetOrAddAsync(cacheKey, fetchFunction, methodName, cancellationToken);

        // A read that overlapped a removal may have fetched the old value; keep it out of memory.
        lock (_sync)
        {
            if (value is not null && _generations.GetValueOrDefault(cacheKey) == generation)
            {
                memoryCache.Set(MemoryKeyPrefix + cacheKey, value, MemoryDuration(methodName));
            }
        }

        return value;
    }

    public async Task RemoveAsync(string cacheKey)
    {
        // Redis first: a local read that starts after the memory copy is dropped must not find the old shared copy.
        await redisCache.RemoveAsync(cacheKey);
        RemoveLocal(cacheKey);
        await PublishAsync(cacheKey);
    }

    protected override void OnRemoteMessage(string payload)
    {
        RemoveLocal(payload);
        Logger.LogInformation("Dropped in-memory cache key {CacheKey} after a change on another Web replica", payload);
    }

    private void RemoveLocal(string cacheKey)
    {
        lock (_sync)
        {
            _generations.AddOrUpdate(cacheKey, 1, static (_, generation) => generation + 1);
            memoryCache.Remove(MemoryKeyPrefix + cacheKey);
        }
    }

    private TimeSpan MemoryDuration(string methodName)
    {
        var memory = cacheSettings.Value.Memory;
        var seconds = memory.Durations.TryGetValue(methodName, out var configured)
            ? configured
            : memory.DefaultDurationInSeconds;
        return TimeSpan.FromSeconds(seconds);
    }
}
