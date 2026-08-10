namespace GovUK.Dfe.FlexForms.Web.Services.Tenant;

/// <summary>
/// Resolves the tenant id for the current HTTP request.
/// </summary>
public interface ITenantIdResolver
{
    /// <summary>
    /// Resolves tenant id from <c>X-Tenant-ID</c>, query string, or request hostname (via platform API).
    /// </summary>
    Task<Guid?> ResolveTenantIdAsync(HttpContext httpContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears cached hostname → tenant mappings (e.g. after tenant settings refresh).
    /// </summary>
    void InvalidateHostnameCache();
}
