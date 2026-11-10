using GovUK.Dfe.CoreLibs.Caching.Settings;
using GovUK.Dfe.FlexForms.Domain.Caching;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace GovUK.Dfe.FlexForms.Infrastructure.Caching;

/// <inheritdoc />
/// <remarks>
/// Value and version live under one hash tag (<c>{cacheKey}</c>) so the Lua scripts touch a single slot on Redis Cluster.
/// </remarks>
public sealed class RedisSharedCacheStore(
    IConnectionMultiplexer redis,
    IOptions<CacheSettings> cacheSettings) : ISharedCacheStore
{
    // Versions are random, so expiry cannot recreate one a fill already saw; a lapsed version only matters to a fill
    // that began before the invalidation and is still running a day later.
    private static readonly TimeSpan VersionLifetime = TimeSpan.FromDays(1);

    // 'or false' because a nil inside a Lua table ends the reply array; false is returned to the client as null.
    private const string ReadScript = """
        return { redis.call('GET', KEYS[1]) or false, redis.call('GET', KEYS[2]) or false }
        """;

    // An empty ARGV[1] means the fill saw no version.
    private const string WriteIfUnchangedScript = """
        if (redis.call('GET', KEYS[2]) or '') == ARGV[1] then
            redis.call('SET', KEYS[1], ARGV[2], 'PX', ARGV[3])
            return 1
        end
        return 0
        """;

    private const string InvalidateScript = """
        redis.call('SET', KEYS[2], ARGV[1], 'PX', ARGV[2])
        redis.call('DEL', KEYS[1])
        return 1
        """;

    private IDatabase Database => redis.GetDatabase(cacheSettings.Value.Redis?.Database ?? -1);

    public async Task<SharedCacheEntry> ReadAsync(string cacheKey)
    {
        var result = (RedisResult[])(await Database.ScriptEvaluateAsync(ReadScript, Keys(cacheKey)))!;
        var value = result[0].IsNull ? null : (byte[]?)result[0];
        var version = result[1].IsNull ? null : (string?)result[1];
        return new SharedCacheEntry(value, version);
    }

    public async Task<bool> WriteIfUnchangedAsync(string cacheKey, byte[] value, string? expectedVersion, TimeSpan expiry)
    {
        var result = await Database.ScriptEvaluateAsync(
            WriteIfUnchangedScript,
            Keys(cacheKey),
            [expectedVersion ?? string.Empty, value, (long)expiry.TotalMilliseconds]);
        return (long)result == 1;
    }

    public Task InvalidateAsync(string cacheKey) =>
        Database.ScriptEvaluateAsync(
            InvalidateScript,
            Keys(cacheKey),
            [Guid.NewGuid().ToString("N"), (long)VersionLifetime.TotalMilliseconds]);

    private static RedisKey[] Keys(string cacheKey)
    {
        var valueKey = $"{FlexFormsCacheKeys.RedisKeyPrefix}{{{cacheKey}}}";
        return [valueKey, $"{valueKey}:version"];
    }
}
