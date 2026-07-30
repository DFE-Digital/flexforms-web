using System.Collections.Concurrent;
using GovUK.Dfe.FlexForms.Web.Configuration;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.Services.Tenant;

/// <inheritdoc />
public sealed class TenantConfigurationCache(IOptions<PlatformBootstrapOptions> options) : ITenantConfigurationCache
{
    private readonly ConcurrentDictionary<Guid, CacheEntry> _cache = new();

    public async Task<PlatformTenantConfigurationResponse> GetOrAddAsync(
        Guid tenantId,
        Func<CancellationToken, Task<PlatformTenantConfigurationResponse>> factory,
        CancellationToken cancellationToken = default)
    {
        var ttl = TimeSpan.FromMinutes(Math.Max(1, options.Value.TenantConfigurationCacheMinutes));
        var now = DateTimeOffset.UtcNow;

        if (_cache.TryGetValue(tenantId, out var existing) && existing.ExpiresAt > now)
        {
            return existing.Value;
        }

        var loaded = await factory(cancellationToken);
        _cache[tenantId] = new CacheEntry(loaded, now.Add(ttl));
        return loaded;
    }

    /// <inheritdoc />
    public void Invalidate(Guid tenantId) => _cache.TryRemove(tenantId, out _);

    private sealed record CacheEntry(PlatformTenantConfigurationResponse Value, DateTimeOffset ExpiresAt);
}
