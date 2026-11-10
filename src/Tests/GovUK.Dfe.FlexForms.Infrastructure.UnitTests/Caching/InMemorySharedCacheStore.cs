using GovUK.Dfe.FlexForms.Infrastructure.Caching;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Caching;

/// <summary>
/// Shared-store double with the same semantics as the Redis Lua scripts, so several replicas can share one "Redis".
/// </summary>
internal sealed class InMemorySharedCacheStore : ISharedCacheStore
{
    private readonly Dictionary<string, byte[]> _values = new();
    private readonly Dictionary<string, long> _versions = new();
    private readonly Lock _sync = new();

    public int Writes { get; private set; }

    public bool HasValue(string cacheKey)
    {
        lock (_sync)
            return _values.ContainsKey(cacheKey);
    }

    public Task<SharedCacheEntry> ReadAsync(string cacheKey)
    {
        lock (_sync)
        {
            return Task.FromResult(new SharedCacheEntry(
                _values.GetValueOrDefault(cacheKey),
                _versions.GetValueOrDefault(cacheKey)));
        }
    }

    public Task<bool> WriteIfUnchangedAsync(string cacheKey, byte[] value, long expectedVersion, TimeSpan expiry)
    {
        lock (_sync)
        {
            if (_versions.GetValueOrDefault(cacheKey) != expectedVersion)
                return Task.FromResult(false);

            _values[cacheKey] = value;
            Writes++;
            return Task.FromResult(true);
        }
    }

    public Task InvalidateAsync(string cacheKey)
    {
        lock (_sync)
        {
            _versions[cacheKey] = _versions.GetValueOrDefault(cacheKey) + 1;
            _values.Remove(cacheKey);
        }

        return Task.CompletedTask;
    }
}
