using System.Collections.Concurrent;
using GovUK.Dfe.FlexForms.Domain.Caching;
using GovUK.Dfe.FlexForms.Web.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace GovUK.Dfe.FlexForms.Web.Services.Tenant;

/// <inheritdoc />
public sealed class TenantConfigurationCache(
    IOptions<PlatformBootstrapOptions> options,
    IConnectionMultiplexer redis,
    ILogger<TenantConfigurationCache> logger) : ITenantConfigurationCache
{
    private readonly ConcurrentDictionary<Guid, CacheEntry> _cache = new();

    public async Task<PlatformTenantConfigurationResponse> GetOrAddAsync(
        Guid tenantId,
        Func<CancellationToken, Task<PlatformTenantConfigurationResponse>> factory,
        CancellationToken cancellationToken = default)
    {
        var ttl = TimeSpan.FromMinutes(Math.Max(1, options.Value.TenantConfigurationCacheMinutes));
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var version = await GetVersionAsync(tenantId, cancellationToken);
            var now = DateTimeOffset.UtcNow;

            if (_cache.TryGetValue(tenantId, out var existing)
                && existing.Version == version && existing.ExpiresAt > now)
                return existing.Value;

            PlatformTenantConfigurationResponse loaded;
            try
            {
                loaded = await factory(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (existing is not null && existing.Version == version)
            {
                if (await GetVersionAsync(tenantId, cancellationToken) != version)
                    continue;

                logger.LogWarning(ex, "Using expired tenant configuration after a platform API load failure for {TenantId}", tenantId);
                return existing.Value;
            }

            // A save may complete while the platform request is in flight.
            if (await GetVersionAsync(tenantId, cancellationToken) != version)
                continue;

            _cache[tenantId] = new CacheEntry(loaded, version, now.Add(ttl));
            return loaded;
        }
    }

    /// <inheritdoc />
    public async Task InvalidateAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var written = await redis.GetDatabase()
            .StringSetAsync(VersionKey(tenantId), Guid.NewGuid().ToString("N"), null, When.Always, CommandFlags.None)
            .WaitAsync(cancellationToken);
        if (!written)
            throw new InvalidOperationException($"Failed to invalidate Web tenant configuration for {tenantId}.");

        _cache.TryRemove(tenantId, out _);
    }

    private async Task<string> GetVersionAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var database = redis.GetDatabase();
        var key = VersionKey(tenantId);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var version = await database.StringGetAsync(key).WaitAsync(cancellationToken);
            if (!version.IsNull)
                return version.ToString();

            // A missing/evicted marker must not match an earlier local cache generation.
            var initialVersion = Guid.NewGuid().ToString("N");
            if (await database.StringSetAsync(key, initialVersion, null, When.NotExists, CommandFlags.None)
                .WaitAsync(cancellationToken))
                return initialVersion;
        }
    }

    private static string VersionKey(Guid tenantId) =>
        $"{FlexFormsCacheKeys.WebTenantConfigurationVersionPrefix}{tenantId:D}";

    private sealed record CacheEntry(PlatformTenantConfigurationResponse Value, string Version, DateTimeOffset ExpiresAt);
}
