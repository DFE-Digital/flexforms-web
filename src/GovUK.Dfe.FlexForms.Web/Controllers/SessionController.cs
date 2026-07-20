using System;
using System.Threading.Tasks;
using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Api.Client.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace GovUK.Dfe.FlexForms.Web.Controllers
{
    /// <summary>
    /// Controller for session management operations including stay-signed-in and sign-out functionality
    /// </summary>
    [Authorize]
    [Route("session")]
    public class SessionController(
        ITokenStateManager tokenStateManager,
        ICacheManager cacheManager,
        IUserActivityTracker activityTracker,
        IAuthenticationSchemeStrategy authStrategy) : Controller
    {
        /// <summary>
        /// Handles the "Stay Signed In" action from the session timeout warning.
        /// Resets the user's activity timestamp and refreshes the authentication token.
        /// </summary>
        [HttpPost("stay-signed-in")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StaySignedIn()
        {
            // Reset the activity timestamp - this is critical to prevent idle timeout!
            activityTracker.RecordActivity(HttpContext);

            cacheManager.SetRequestScopedFlag("AllowRefreshDueToInactivity", true);

            // Try to refresh token using the auth strategy (handles OIDC, Test, Internal auth)
            await authStrategy.RefreshExternalIdpTokenAsync(HttpContext);

            // Also call the token state manager for consistency
            await tokenStateManager.RefreshTokensIfPossibleAsync();

            return Redirect("/applications/dashboard");
        }

        /// <summary>
        /// Handles immediate sign-out request from the session timeout warning.
        /// Clears all authentication state and redirects to home.
        /// </summary>
        [HttpPost("sign-out")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignOutImmediately()
        {
            await tokenStateManager.ForceCompleteLogoutAsync();

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            var authScheme = User?.Identity?.AuthenticationType;
            var usingOidc = string.Equals(authScheme, "AuthenticationTypes.Federation", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(authScheme, OpenIdConnectDefaults.AuthenticationScheme, StringComparison.OrdinalIgnoreCase);
            if (usingOidc)
            {
                try
                {
                    await HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme, new AuthenticationProperties
                    {
                        RedirectUri = "/"
                    });
                }
                catch
                {
                    // ignore if OIDC not configured in current environment
                }
            }

            HttpContext.Session.Clear();

            return Redirect("/");
        }
    }
}
