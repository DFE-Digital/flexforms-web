using System.Collections.Concurrent;
using GovUK.Dfe.FlexForms.Web.Configuration;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.Services.Tenant;

/// <inheritdoc />
public sealed class TenantConfigurationCache(IOptions<PlatformBootstrapOptions> options) : ITenantConfigurationCache
{
    private readonly ConcurrentDictionary<Guid, CacheEntry> _cache = new();
    private readonly ConcurrentDictionary<Guid, long> _generations = new();
    private readonly Lock _sync = new();

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

        var generation = _generations.GetValueOrDefault(tenantId);

        try
        {
            var loaded = await factory(cancellationToken);

            // A load that overlaps an invalidation may have read the configuration from before the change.
            lock (_sync)
            {
                if (_generations.GetValueOrDefault(tenantId) == generation)
                {
                    _cache[tenantId] = new CacheEntry(loaded, now.Add(ttl));
                }
            }

            return loaded;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception) when (existing is not null)
        {
            return existing.Value;
        }
    }

    /// <inheritdoc />
    public void Invalidate(Guid tenantId)
    {
        lock (_sync)
        {
            _generations.AddOrUpdate(tenantId, 1, static (_, generation) => generation + 1);
            _cache.TryRemove(tenantId, out _);
        }
    }

    private sealed record CacheEntry(PlatformTenantConfigurationResponse Value, DateTimeOffset ExpiresAt);
}
