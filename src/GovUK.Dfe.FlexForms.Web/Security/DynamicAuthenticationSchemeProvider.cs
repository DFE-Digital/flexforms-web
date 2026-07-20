using GovUK.Dfe.FlexForms.Web.Authentication;
using GovUK.Dfe.CoreLibs.Security.Configurations;
using GovUK.Dfe.CoreLibs.Security.EntraSso;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Selects the active authentication scheme per request with forwarder pattern.
/// Priority order:
/// 1. If X-Service-Email header present: Uses Internal Service Auth (header-based forwarder)
/// 2. If TestAuthentication.Enabled is true: Uses Test scheme for all
/// 3. If tenant (or host) EntraSso.Enabled is true: Uses Entra SSO scheme
/// 4. Otherwise: Uses DfE Sign-In OIDC (Cookies + OIDC challenge/sign-out)
/// </summary>
public class DynamicAuthenticationSchemeProvider(
    IOptions<AuthenticationOptions> options,
    IHttpContextAccessor httpContextAccessor,
    IOptions<TestAuthenticationOptions> testAuthOptions,
    IOptions<EntraSsoOptions> entraSsoOptions)
    : AuthenticationSchemeProvider(options)
{
    private bool IsTestAuthGloballyEnabled() => testAuthOptions.Value.Enabled;

    private bool IsEntraSsoEnabled()
        => TenantAuthSchemeSelector.IsEntraSsoEnabled(httpContextAccessor.HttpContext, entraSsoOptions);

    private bool IsInternalServiceRequest()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext == null) return false;

        return httpContext.Request.Headers.ContainsKey("x-service-email");
    }

    private string GetDefaultIdpScheme()
    {
        return IsEntraSsoEnabled()
            ? EntraSsoDefaults.AuthenticationScheme
            : OpenIdConnectDefaults.AuthenticationScheme;
    }

    public override Task<AuthenticationScheme?> GetDefaultAuthenticateSchemeAsync()
    {
        if (IsInternalServiceRequest())
        {
            return GetSchemeAsync(InternalServiceAuthenticationHandler.SchemeName);
        }

        if (IsTestAuthGloballyEnabled())
        {
            return GetSchemeAsync(TestAuthenticationHandler.SchemeName);
        }

        return GetSchemeAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    public override Task<AuthenticationScheme?> GetDefaultChallengeSchemeAsync()
    {
        if (IsInternalServiceRequest())
        {
            return GetSchemeAsync(InternalServiceAuthenticationHandler.SchemeName);
        }

        if (IsTestAuthGloballyEnabled())
        {
            return GetSchemeAsync(TestAuthenticationHandler.SchemeName);
        }

        return GetSchemeAsync(GetDefaultIdpScheme());
    }

    public override Task<AuthenticationScheme?> GetDefaultForbidSchemeAsync()
    {
        if (IsInternalServiceRequest())
        {
            return GetSchemeAsync(InternalServiceAuthenticationHandler.SchemeName);
        }

        if (IsTestAuthGloballyEnabled())
        {
            return GetSchemeAsync(TestAuthenticationHandler.SchemeName);
        }

        return GetSchemeAsync(GetDefaultIdpScheme());
    }

    public override Task<AuthenticationScheme?> GetDefaultSignInSchemeAsync()
    {
        return GetSchemeAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    public override Task<AuthenticationScheme?> GetDefaultSignOutSchemeAsync()
    {
        // Always the application cookie. Remote IdP sign-out is triggered explicitly
        // (e.g. Logout page). Returning the IdP scheme here prevents cookie clearance
        // when OIDC uses SignOutScheme / default sign-out.
        return GetSchemeAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
