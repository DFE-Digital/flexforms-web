using System.Diagnostics.CodeAnalysis;
using GovUK.Dfe.CoreLibs.Security.Configurations;
using GovUK.Dfe.CoreLibs.Security.EntraSso;
using GovUK.Dfe.FlexForms.Api.Client.Security;
using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.Controllers;

/// <summary>
/// Session keep-alive and timeout sign-out. Works for DfE Sign-In (OIDC) and Entra SSO.
/// </summary>
[Authorize]
[Route("session")]
[ExcludeFromCodeCoverage]
public class SessionController(
    ITokenStateManager tokenStateManager,
    IUserActivityTracker activityTracker,
    IAuthenticationSchemeStrategy authStrategy,
    IOptions<EntraSsoOptions> entraSsoOptions,
    IOptions<TestAuthenticationOptions> testAuthOptions,
    ILogger<SessionController> logger,
    ITestAuthenticationService? testAuthenticationService = null) : Controller
{
    /// <summary>
    /// "Stay signed in" from the inactivity warning (POST from the banner form / fetch).
    /// Resets idle activity and best-effort refreshes tokens (OIDC). Always succeeds for idle reset.
    /// </summary>
    [HttpPost("stay-signed-in")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> StaySignedInPost(string? returnUrl = null)
        => StaySignedInCoreAsync(returnUrl, allowJson: true);

    /// <summary>
    /// Safe landing after an IDP challenge. Cookie auth / Entra often re-issue a <c>GET</c> to the
    /// original URL after login; a POST-only action would return 405. This resets activity and
    /// sends the user back to their page (or the dashboard).
    /// </summary>
    [HttpGet("stay-signed-in")]
    public IActionResult StaySignedInGet(string? returnUrl = null)
    {
        activityTracker.RecordActivity(HttpContext);
        return RedirectAfterStaySignedIn(returnUrl);
    }

    private async Task<IActionResult> StaySignedInCoreAsync(string? returnUrl, bool allowJson)
    {
        activityTracker.RecordActivity(HttpContext);

        try
        {
            await authStrategy.RefreshExternalIdpTokenAsync(HttpContext);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "IDP token refresh failed during stay-signed-in; idle timer was still reset.");
        }

        try
        {
            await tokenStateManager.RefreshTokensIfPossibleAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "API token refresh failed during stay-signed-in; idle timer was still reset.");
        }

        if (allowJson && IsAjaxRequest())
            return Ok(new { ok = true });

        return RedirectAfterStaySignedIn(returnUrl);
    }

    private IActionResult RedirectAfterStaySignedIn(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            && !returnUrl.StartsWith("/session/", StringComparison.OrdinalIgnoreCase))
        {
            return LocalRedirect(returnUrl);
        }

        return Redirect("/applications/dashboard");
    }

    /// <summary>
    /// Explicit "Sign out" from the timeout banner — full IDP sign-out.
    /// </summary>
    [HttpPost("sign-out")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SignOutImmediately() => SignOutForAllIdpsAsync();

    /// <summary>
    /// Auto sign-out when the inactivity countdown reaches zero (form POST from banner JS).
    /// </summary>
    [HttpPost("timeout-sign-out")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> TimeoutSignOutPost() => SignOutForAllIdpsAsync();

    /// <summary>
    /// Used by middleware when idle/absolute/token limits are hit.
    /// Performs full cookie + IDP sign-out (same as the Logout page).
    /// </summary>
    [HttpGet("timeout-sign-out")]
    public Task<IActionResult> TimeoutSignOutGet(string? reason = null)
    {
        logger.LogInformation("Timeout sign-out requested. Reason: {Reason}", reason ?? "unspecified");
        return SignOutForAllIdpsAsync();
    }

    private async Task<IActionResult> SignOutForAllIdpsAsync()
    {
        try
        {
            await tokenStateManager.ForceCompleteLogoutAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clear token caches during session sign-out");
        }

        var interactiveScheme = TenantAuthSchemeSelector.Resolve(
            HttpContext,
            testAuthOptions,
            entraSsoOptions);

        if (interactiveScheme == InteractiveAuthScheme.TestAuthentication && testAuthenticationService != null)
        {
            HttpContext.Session.Clear();
            await testAuthenticationService.SignOutAsync(HttpContext);
            return Redirect(DfESignInOidcPublicUrls.BuildAbsoluteUrl(HttpContext, "/"));
        }

        HttpContext.Session.Clear();

        var homeUrl = DfESignInOidcPublicUrls.BuildAbsoluteUrl(HttpContext, "/");
        var signOutProperties = new AuthenticationProperties { RedirectUri = homeUrl };

        if (interactiveScheme == InteractiveAuthScheme.EntraSso)
        {
            logger.LogInformation("Session timeout sign-out via Entra SSO");
            return SignOut(
                signOutProperties,
                EntraSsoDefaults.AuthenticationScheme,
                CookieAuthenticationDefaults.AuthenticationScheme);
        }

        logger.LogInformation("Session timeout sign-out via OpenID Connect");
        return SignOut(
            signOutProperties,
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    private bool IsAjaxRequest()
    {
        if (string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            return true;

        var accept = Request.Headers.Accept.ToString();
        return accept.Contains("application/json", StringComparison.OrdinalIgnoreCase);
    }
}
