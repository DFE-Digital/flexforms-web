using System.Collections.Concurrent;
using GovUK.Dfe.FlexForms.Web.Configuration;
using GovUK.Dfe.FlexForms.Web.Services.Platform;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.Services.Tenant;

/// <inheritdoc />
public sealed class TenantIdResolver(
    PlatformConfigurationApiClient apiClient,
    IOptions<PlatformBootstrapOptions> options,
    ILogger<TenantIdResolver> logger) : ITenantIdResolver
{
    public const string TenantIdHeader = "X-Tenant-ID";

    private readonly ConcurrentDictionary<string, CacheEntry> _hostnameCache = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task<Guid?> ResolveTenantIdAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        if (httpContext.Request.Headers.TryGetValue(TenantIdHeader, out var headerValue) &&
            Guid.TryParse(headerValue, out var tenantFromHeader))
        {
            return tenantFromHeader;
        }

        if (httpContext.Request.Query.TryGetValue("tenantId", out var queryValue) &&
            Guid.TryParse(queryValue, out var tenantFromQuery))
        {
            return tenantFromQuery;
        }

        var host = httpContext.Request.Host.Host;
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var ttl = TimeSpan.FromMinutes(Math.Max(1, options.Value.TenantConfigurationCacheMinutes));
        var now = DateTimeOffset.UtcNow;
        if (_hostnameCache.TryGetValue(host, out var cached) && cached.ExpiresAt > now)
        {
            return cached.TenantId;
        }

        try
        {
            var resolution = await apiClient.ResolveTenantByHostnameAsync(host, cancellationToken);
            logger.LogDebug(
                "Resolved tenant {TenantId} ({TenantName}) from hostname {Hostname}",
                resolution.TenantId,
                resolution.TenantName,
                resolution.Hostname);

            _hostnameCache[host] = new CacheEntry(resolution.TenantId, now.Add(ttl));
            return resolution.TenantId;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Could not resolve tenant for hostname {Hostname}", host);
            return null;
        }
    }

    private sealed record CacheEntry(Guid TenantId, DateTimeOffset ExpiresAt);
}
