using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
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

        var forwardedHost = httpContext.Request.Headers["X-Forwarded-Host"].ToString();
        var originalHost = httpContext.Request.Headers["X-Original-Host"].ToString();
        var requestHost = httpContext.Request.Host.Value;
        var requestHostHost = httpContext.Request.Host.Host;

        var host = ResolvePublicHostname(httpContext);

        logger.LogInformation(
            "Tenant hostname resolution headers: X-Forwarded-Host={ForwardedHost}, X-Original-Host={OriginalHost}, Request.Host={RequestHost}, Request.Host.Host={RequestHostHost}, ChosenHost={ChosenHost}",
            string.IsNullOrWhiteSpace(forwardedHost) ? "(empty)" : forwardedHost,
            string.IsNullOrWhiteSpace(originalHost) ? "(empty)" : originalHost,
            string.IsNullOrWhiteSpace(requestHost) ? "(empty)" : requestHost,
            string.IsNullOrWhiteSpace(requestHostHost) ? "(empty)" : requestHostHost,

            string.IsNullOrWhiteSpace(host) ? "(none)" : host);

        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogWarning(
                "Could not resolve a public hostname for tenant lookup (Request.Host={RequestHost}, X-Forwarded-Host={ForwardedHost}, X-Original-Host={OriginalHost})",
                requestHost,
                string.IsNullOrWhiteSpace(forwardedHost) ? "(empty)" : forwardedHost,
                string.IsNullOrWhiteSpace(originalHost) ? "(empty)" : originalHost);
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

    /// <summary>
    /// Prefers a public tenant hostname over Azure Container Apps / private hosts.
    /// Order: <c>X-Forwarded-Host</c>, then <c>Request.Host</c>, then <c>X-Original-Host</c>.
    /// ACA often puts its own FQDN in <c>X-Original-Host</c> while <c>Request.Host</c> is already public.
    /// </summary>
    public static string? ResolvePublicHostname(HttpContext httpContext)
    {
        foreach (var candidate in EnumerateHostnameCandidates(httpContext))
        {
            if (IsUsablePublicHostname(candidate))
                return candidate;
        }

        return null;
    }

    private static IEnumerable<string> EnumerateHostnameCandidates(HttpContext httpContext)
    {
        // 1) X-Forwarded-Host — correct when App Gateway rewrite sets the public host.
        yield return FirstHostFromHeader(httpContext.Request.Headers["X-Forwarded-Host"]);

        // 2) Request.Host — often already the public hostname after AGW / ACA (e.g. visits.dev-flexforms...).
        // Prefer this over X-Original-Host: Azure commonly sets X-Original-Host to the ACA backend FQDN.
        yield return httpContext.Request.Host.Host;

        // 3) X-Original-Host — last resort (may be the container app hostname).
        yield return FirstHostFromHeader(httpContext.Request.Headers["X-Original-Host"]);
    }

    private static string? FirstHostFromHeader(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
            return null;

        // X-Forwarded-Host may be a comma-separated list; leftmost is the original client host.
        var first = headerValue.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(first))
            return null;

        // Strip optional port
        var hostOnly = first.Split(':', 2)[0].Trim();
        return string.IsNullOrWhiteSpace(hostOnly) ? null : hostOnly;
    }

    private static bool IsUsablePublicHostname(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Container Apps default FQDN is never a tenant hostname mapping.
        if (host.EndsWith(".azurecontainerapps.io", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!IPAddress.TryParse(host, out var ip))
            return true; // DNS name

        // Never resolve tenants from container/private probe IPs (e.g. 172.16.x.x).
        return !IsPrivateOrLocalIp(ip);
    }

    private static bool IsPrivateOrLocalIp(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
            return true;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            return bytes[0] switch
            {
                10 => true,
                127 => true,
                169 when bytes[1] == 254 => true, // link-local
                172 when bytes[1] >= 16 && bytes[1] <= 31 => true,
                192 when bytes[1] == 168 => true,
                _ => false
            };
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal;
        }

        return false;
    }

    private sealed record CacheEntry(Guid TenantId, DateTimeOffset ExpiresAt);
}
