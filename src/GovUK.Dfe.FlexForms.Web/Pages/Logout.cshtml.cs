using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using GovUK.Dfe.CoreLibs.Security.Configurations;
using GovUK.Dfe.CoreLibs.Security.EntraSso;
using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Web.Services;
using System.Diagnostics.CodeAnalysis;

namespace GovUK.Dfe.FlexForms.Web.Pages;

[ExcludeFromCodeCoverage]
[AllowAnonymous]
public class LogoutModel(
    IOptions<TestAuthenticationOptions> testAuthOptions,
    IOptions<EntraSsoOptions> entraSsoOptions,
    ILogger<LogoutModel> logger,
    ITestAuthenticationService? testAuthenticationService = null) : PageModel
{
    public IActionResult OnGet()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return RedirectToPage("/Applications/Dashboard");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            if (testAuthOptions.Value.Enabled && testAuthenticationService != null)
            {
                logger.LogInformation("Signing out from test authentication");
                HttpContext.Session.Clear();
                await testAuthenticationService.SignOutAsync(HttpContext);
                return Redirect(DfESignInOidcPublicUrls.BuildAbsoluteUrl(HttpContext, "/"));
            }

            // Cookie must be signed out explicitly — OIDC HandleSignOutCallbackAsync does not
            // clear it. RedirectUri must stay on the current tenant host (not bootstrap localhost).
            //
            // Entra needs id_token_hint from the cookie auth ticket, so sign out the remote
            // scheme first (reads the token), then clear the cookie in the same response.
            var homeUrl = DfESignInOidcPublicUrls.BuildAbsoluteUrl(HttpContext, "/");
            var signOutProperties = new AuthenticationProperties { RedirectUri = homeUrl };

            if (TenantAuthSchemeSelector.IsEntraSsoEnabled(HttpContext, entraSsoOptions))
            {
                logger.LogInformation("Signing out from Entra SSO authentication");

                return SignOut(
                    signOutProperties,
                    EntraSsoDefaults.AuthenticationScheme,
                    CookieAuthenticationDefaults.AuthenticationScheme);
            }

            logger.LogInformation("Signing out from DfE Sign-In OIDC authentication");

            return SignOut(
                signOutProperties,
                CookieAuthenticationDefaults.AuthenticationScheme,
                OpenIdConnectDefaults.AuthenticationScheme);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during sign out process");
            ModelState.AddModelError(string.Empty, "An error occurred while signing out. Please try again.");
            return Page();
        }
    }
}
