using GovUK.Dfe.CoreLibs.Security.Configurations;
using GovUK.Dfe.CoreLibs.Security.Interfaces;
using GovUK.Dfe.FlexForms.Api.Client.Security;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Chooses the appropriate authentication strategy per-request.
/// Priority: Internal Auth > Test Auth > Entra SSO (when tenant/host enabled) > DfE Sign-In OIDC
/// </summary>
public class CompositeAuthenticationSchemeStrategy(
    ILogger<CompositeAuthenticationSchemeStrategy> logger,
    IHttpContextAccessor httpContextAccessor,
    IOptions<TestAuthenticationOptions> testAuthOptions,
    IOptions<EntraSsoOptions> entraSsoOptions,
    OidcAuthenticationStrategy oidcStrategy,
    TestAuthenticationStrategy testStrategy,
    InternalAuthenticationStrategy internalStrategy,
    EntraSsoAuthenticationStrategy entraSsoStrategy,
    [FromKeyedServices("internal")] ICustomRequestChecker internalRequestChecker)
    : IAuthenticationSchemeStrategy
{
    private bool IsTestEnabled() => testAuthOptions.Value.Enabled;

    private bool IsEntraSsoEnabled()
        => TenantAuthSchemeSelector.IsEntraSsoEnabled(httpContextAccessor.HttpContext, entraSsoOptions);

    private bool IsInternalAuthRequest()
    {
        var ctx = httpContextAccessor.HttpContext;
        if (ctx == null) return false;
        return internalRequestChecker != null && internalRequestChecker.IsValidRequest(ctx);
    }

    private IAuthenticationSchemeStrategy Select()
    {
        var ctx = httpContextAccessor.HttpContext;
        var path = ctx?.Request.Path.ToString() ?? "unknown";

        if (IsInternalAuthRequest())
        {
            logger.LogDebug("Selecting InternalAuthenticationStrategy for {Path}.", path);
            return internalStrategy;
        }

        if (IsTestEnabled())
        {
            logger.LogDebug("Selecting TestAuthenticationStrategy for {Path}.", path);
            return testStrategy;
        }

        if (IsEntraSsoEnabled())
        {
            logger.LogDebug("Selecting EntraSsoAuthenticationStrategy for {Path}.", path);
            return entraSsoStrategy;
        }

        logger.LogDebug("Selecting OidcAuthenticationStrategy for {Path}", path);
        return oidcStrategy;
    }

    public string SchemeName => Select().SchemeName;

    public Task<TokenInfo> GetExternalIdpTokenAsync(HttpContext context)
        => Select().GetExternalIdpTokenAsync(context);

    public Task<bool> CanRefreshTokenAsync(HttpContext context)
        => Select().CanRefreshTokenAsync(context);

    public Task<bool> RefreshExternalIdpTokenAsync(HttpContext context)
        => Select().RefreshExternalIdpTokenAsync(context);

    public string? GetUserId(HttpContext context)
        => Select().GetUserId(context);
}
