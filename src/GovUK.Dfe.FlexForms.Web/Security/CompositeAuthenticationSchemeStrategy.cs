using GovUK.Dfe.CoreLibs.Security.Configurations;
using GovUK.Dfe.CoreLibs.Security.Interfaces;
using GovUK.Dfe.FlexForms.Api.Client.Security;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Chooses the appropriate authentication strategy per-request.
/// Priority: Internal Auth > tenant/host interactive scheme
/// (explicit Authentication:Scheme, else Test / Entra / DfE Sign-In).
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
    private InteractiveAuthScheme ResolveInteractiveScheme()
        => TenantAuthSchemeSelector.Resolve(
            httpContextAccessor.HttpContext,
            testAuthOptions,
            entraSsoOptions);

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

        return ResolveInteractiveScheme() switch
        {
            InteractiveAuthScheme.TestAuthentication => LogAndReturn(testStrategy, path),
            InteractiveAuthScheme.EntraSso => LogAndReturn(entraSsoStrategy, path),
            _ => LogAndReturn(oidcStrategy, path)
        };
    }

    private IAuthenticationSchemeStrategy LogAndReturn(IAuthenticationSchemeStrategy strategy, string path)
    {
        logger.LogDebug("Selecting {Strategy} for {Path}.", strategy.GetType().Name, path);
        return strategy;
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
