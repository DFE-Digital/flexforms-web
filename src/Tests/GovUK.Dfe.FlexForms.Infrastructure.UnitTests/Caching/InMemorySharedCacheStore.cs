using GovUK.Dfe.FlexForms.Infrastructure.Caching;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Caching;

/// <summary>
/// Shared-store double with the same semantics as the Redis Lua scripts, so several replicas can share one "Redis".
/// The <c>Fail*</c> switches make individual operations throw, as Redis does when it is unreachable.
/// </summary>
internal sealed class InMemorySharedCacheStore : ISharedCacheStore
{
    private readonly Dictionary<string, byte[]> _values = new();
    private readonly Dictionary<string, string> _versions = new();
    private readonly Lock _sync = new();

    public int Writes { get; private set; }

    public TimeSpan? LastWriteExpiry { get; private set; }

    public bool FailReads { get; set; }

    public bool FailWrites { get; set; }

    public bool FailInvalidations { get; set; }

    public bool HasValue(string cacheKey)
    {
        lock (_sync)
            return _values.ContainsKey(cacheKey);
    }

    public void Put(string cacheKey, byte[] value)
    {
        lock (_sync)
            _values[cacheKey] = value;
    }

    /// <summary>Simulates the version key's expiry.</summary>
    public void ExpireVersion(string cacheKey)
    {
        lock (_sync)
            _versions.Remove(cacheKey);
    }

    public Task<SharedCacheEntry> ReadAsync(string cacheKey)
    {
        ThrowIf(FailReads);
        lock (_sync)
        {
            return Task.FromResult(new SharedCacheEntry(
                _values.GetValueOrDefault(cacheKey),
                _versions.GetValueOrDefault(cacheKey)));
        }
    }

    public Task<bool> WriteIfUnchangedAsync(string cacheKey, byte[] value, string? expectedVersion, TimeSpan expiry)
    {
        ThrowIf(FailWrites);
        lock (_sync)
        {
            if (_versions.GetValueOrDefault(cacheKey) != expectedVersion)
                return Task.FromResult(false);

            _values[cacheKey] = value;
            Writes++;
            LastWriteExpiry = expiry;
            return Task.FromResult(true);
        }
    }

    public Task InvalidateAsync(string cacheKey)
    {
        ThrowIf(FailInvalidations);
        lock (_sync)
        {
            _versions[cacheKey] = Guid.NewGuid().ToString("N");
            _values.Remove(cacheKey);
        }

        return Task.CompletedTask;
    }

    private static void ThrowIf(bool fail)
    {
        if (fail)
            throw new StackExchange.Redis.RedisConnectionException(
                StackExchange.Redis.ConnectionFailureType.UnableToConnect, "Redis is down");
    }
}
