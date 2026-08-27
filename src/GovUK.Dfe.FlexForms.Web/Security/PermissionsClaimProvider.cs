using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.CoreLibs.Security.Interfaces;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Web.Security;

[ExcludeFromCodeCoverage]
public class PermissionsClaimProvider(
    IMemoryCache cache,
    IHttpContextAccessor httpContextAccessor) : ICustomClaimProvider
{
    public Task<IEnumerable<Claim>> GetClaimsAsync(ClaimsPrincipal principal)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Task.FromResult(Enumerable.Empty<Claim>());

        var tenantId = httpContextAccessor.HttpContext?.RequestServices
            .GetService(typeof(ITenantRequestContext)) is ITenantRequestContext tenantContext
            ? tenantContext.TenantId
            : null;

        var cacheKey = UserPermissionsCache.GetCacheKey(principal, tenantId);
        
        var claims = new List<Claim>();

        if (cache.TryGetValue(cacheKey, out UserAuthorizationDto? authData) && authData != null)
        {
            if (authData.Permissions?.Any() == true)
            {
                claims.AddRange(authData.Permissions.Select(p =>
                    new Claim(
                        "permission",
                        $"{p.ResourceType}:{p.ResourceKey}:{p.AccessType}"
                    )));
            }

            if (authData.Roles?.Any() == true)
            {
                claims.AddRange(authData.Roles.Select(role =>
                    new Claim(ClaimTypes.Role, role)));
            }

            return Task.FromResult<IEnumerable<Claim>>(claims);
        }

        return Task.FromResult(Enumerable.Empty<Claim>());
    }
}
