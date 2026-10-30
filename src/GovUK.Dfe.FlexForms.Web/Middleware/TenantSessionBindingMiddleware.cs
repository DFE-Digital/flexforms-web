using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace GovUK.Dfe.FlexForms.Web.Middleware;

/// <summary>
/// Rejects an authenticated session whose <c>tenant_id</c> claim does not match the hostname-resolved tenant.
/// Cookies minted before this claim existed are allowed through until the user next signs in.
/// </summary>
public sealed class TenantSessionBindingMiddleware(RequestDelegate next, ILogger<TenantSessionBindingMiddleware> logger)
{
    public const string TenantIdClaimType = "tenant_id";

    public async Task InvokeAsync(HttpContext context, ITenantRequestContext tenantRequestContext)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && tenantRequestContext.TenantId is Guid resolved)
        {
            var sessionClaim = context.User.FindFirst(TenantIdClaimType)?.Value;
            if (Guid.TryParse(sessionClaim, out var sessionTenant) && sessionTenant != resolved)
            {
                logger.LogWarning(
                    "Rejecting session for tenant {SessionTenant} on hostname tenant {ResolvedTenant}",
                    sessionTenant,
                    resolved);

                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                context.Response.Redirect("/Error/Forbidden");
                return;
            }
        }

        await next(context);
    }
}
