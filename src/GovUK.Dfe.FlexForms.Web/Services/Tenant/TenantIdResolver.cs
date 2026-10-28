using System.Net;
using System.Net.Sockets;
using GovUK.Dfe.FlexForms.Web.Configuration;
using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Web.Services.Platform;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.Services.Tenant;

/// <inheritdoc />
public sealed class TenantIdResolver(
    PlatformConfigurationApiClient apiClient,
    TenantHostnameCache hostnameCache,
    IOptions<PlatformBootstrapOptions> options,
    ILogger<TenantIdResolver> logger,
    IHostEnvironment? hostEnvironment = null) : ITenantIdResolver
{
    public const string TenantIdHeader = "X-Tenant-ID";

    /// <summary>
    /// HttpContext.Items key for <see cref="HttpRequest.Host"/> captured before
    /// <c>UseForwardedHeaders</c> rewrites it from <c>X-Forwarded-Host</c>.
    /// </summary>
    public const string OriginalRequestHostItemKey = "FlexForms.OriginalRequestHost";

    /// <inheritdoc />
    public void InvalidateHostnameCache()
    {
        hostnameCache.Clear();
        logger.LogDebug("Tenant hostname cache cleared.");
    }

    /// <inheritdoc />
    public async Task<Guid?> ResolveTenantIdAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        if (HasConflictingPublicHostnames(httpContext))
        {
            throw new TenantHostnameConflictException(
                "X-Forwarded-Host and Request.Host are both public hostnames and they differ.");
        }

        var forwardedHost = httpContext.Request.Headers["X-Forwarded-Host"].ToString();
        var originalHost = httpContext.Request.Headers["X-Original-Host"].ToString();
        var requestHost = httpContext.Request.Host.Value;
        var requestHostHost = httpContext.Request.Host.Host;

        var host = ResolvePublicHostname(httpContext);

        logger.LogDebug(
            "Tenant hostname resolution headers: X-Forwarded-Host={ForwardedHost}, X-Original-Host={OriginalHost}, Request.Host={RequestHost}, Request.Host.Host={RequestHostHost}, ChosenHost={ChosenHost}",
            string.IsNullOrWhiteSpace(forwardedHost) ? "(empty)" : forwardedHost,
            string.IsNullOrWhiteSpace(originalHost) ? "(empty)" : originalHost,
            string.IsNullOrWhiteSpace(requestHost) ? "(empty)" : requestHost,
            string.IsNullOrWhiteSpace(requestHostHost) ? "(empty)" : requestHostHost,
            string.IsNullOrWhiteSpace(host) ? "(none)" : host);

        Guid? fromHost = null;
        if (!string.IsNullOrWhiteSpace(host))
        {
            fromHost = await ResolveFromHostnameAsync(host, cancellationToken);
        }

        var explicitId = TryReadExplicitTenantId(httpContext);

        if (fromHost is Guid hostId)
        {
            if (explicitId is Guid headerId && headerId != hostId)
            {
                // Local development uses localhost plus X-Tenant-ID to pick a tenant.
                if (IsLocalDevelopmentHostname(host) && AllowsExplicitTenantSelection())
                    return headerId;

                logger.LogWarning(
                    "Ignoring X-Tenant-ID {HeaderTenant} because hostname {Hostname} resolved tenant {HostTenant}",
                    headerId,
                    host,
                    hostId);
            }

            return hostId;
        }

        if (explicitId is Guid id && AllowsExplicitTenantSelection())
            return id;

        return null;
    }

    /// <summary>
    /// Uses <see cref="HttpRequest.Host"/> after <c>UseForwardedHeaders</c> has applied
    /// the trusted-proxy policy. Raw <c>X-Forwarded-Host</c> / <c>X-Original-Host</c> are
    /// not read here so an untrusted client cannot pick a tenant by header.
    /// </summary>
    public static string? ResolvePublicHostname(HttpContext httpContext)
    {
        var host = UnbracketIpv6(httpContext.Request.Host.Host);
        return IsUsablePublicHostname(host) ? host : null;
    }

    /// <summary>
    /// True when the request presents more than one distinct usable public hostname.
    /// Compares the pre-forward <see cref="HttpRequest.Host"/>, the current host
    /// (after <c>UseForwardedHeaders</c> may have rewritten it), and <c>X-Forwarded-Host</c>
    /// if that header is still present.
    /// </summary>
    public static bool HasConflictingPublicHostnames(HttpContext httpContext)
    {
        string?[] candidates =
        [
            OriginalRequestHost(httpContext),
            UnbracketIpv6(httpContext.Request.Host.Host),
            FirstHostFromHeader(httpContext.Request.Headers["X-Forwarded-Host"])
        ];

        string? seen = null;
        foreach (var candidate in candidates)
        {
            if (!IsUsablePublicHostname(candidate))
                continue;

            if (seen is null)
            {
                seen = candidate;
                continue;
            }

            if (!string.Equals(seen, candidate, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string? OriginalRequestHost(HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(OriginalRequestHostItemKey, out var stored)
            && stored is string host
            && !string.IsNullOrWhiteSpace(host))
        {
            return UnbracketIpv6(host);
        }

        return UnbracketIpv6(httpContext.Request.Host.Host);
    }

    /// <summary>
    /// True when the request arrived on the Container Apps FQDN (or another non-tenant host)
    /// with no public hostname header. Typical of probes and internal calls, not browsers.
    /// </summary>
    public static bool IsNonPublicHostRequest(HttpContext httpContext) =>
        string.IsNullOrWhiteSpace(ResolvePublicHostname(httpContext));

    private async Task<Guid?> ResolveFromHostnameAsync(
        string host,
        CancellationToken cancellationToken)
    {
        var ttl = TimeSpan.FromMinutes(Math.Max(1, options.Value.TenantConfigurationCacheMinutes));
        var now = DateTimeOffset.UtcNow;
        if (hostnameCache.TryGet(host, now, allowExpired: false, out var cached))
        {
            return cached;
        }

        return await hostnameCache.RunSerializedAsync<Guid?>(host, async () =>
        {
            now = DateTimeOffset.UtcNow;
            if (hostnameCache.TryGet(host, now, allowExpired: false, out var winner))
            {
                return winner;
            }

            try
            {
                var resolution = await apiClient.ResolveTenantByHostnameAsync(host, cancellationToken);
                logger.LogDebug(
                    "Resolved tenant {TenantId} ({TenantName}) from hostname {Hostname}",
                    resolution.TenantId,
                    resolution.TenantName,
                    resolution.Hostname);

                hostnameCache.Set(host, resolution.TenantId, now.Add(ttl));
                return resolution.TenantId;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                if (hostnameCache.TryGet(host, now, allowExpired: true, out var stale))
                {
                    logger.LogWarning(
                        ex,
                        "Platform tenant resolve failed for {Hostname}; using cached tenant {TenantId}",
                        host,
                        stale);
                    return stale;
                }

                logger.LogWarning(ex, "Could not resolve tenant for hostname {Hostname}", host);
                return null;
            }
        }, cancellationToken);
    }

    private static Guid? TryReadExplicitTenantId(HttpContext httpContext)
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

        return null;
    }

    private bool AllowsExplicitTenantSelection()
        => hostEnvironment is not null
           && TestAuthenticationEnvironmentGate.IsAllowed(hostEnvironment);

    private static bool IsLocalDevelopmentHostname(string? host)
        => !string.IsNullOrWhiteSpace(host)
           && (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
               || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase));

    private static string? FirstHostFromHeader(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
            return null;

        // X-Forwarded-Host may be a comma-separated list; leftmost is the original client host.
        var first = headerValue.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(first))
            return null;

        HostString hostString;
        try
        {
            hostString = HostString.FromUriComponent(first);
        }
        catch (Exception)
        {
            return null;
        }

        var hostOnly = UnbracketIpv6(hostString.Host);
        return string.IsNullOrWhiteSpace(hostOnly) ? null : hostOnly;
    }

    private static string? UnbracketIpv6(string? host)
    {
        if (string.IsNullOrEmpty(host) || host.Length < 2 || host[0] != '[' || host[^1] != ']')
            return host;

        return host[1..^1];
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
}
