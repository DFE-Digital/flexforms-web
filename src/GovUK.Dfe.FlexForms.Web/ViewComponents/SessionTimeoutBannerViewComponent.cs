using System;
using System.Threading.Tasks;
using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Api.Client.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.ViewComponents
{
    /// <summary>
    /// View component that displays a session timeout warning banner.
    /// Shows a warning when the user is close to being logged out due to inactivity or token expiry.
    /// </summary>
    public class SessionTimeoutBannerViewComponent(
        ITokenStateManager tokenStateManager,
        IUserActivityTracker activityTracker,
        IHttpContextAccessor httpContextAccessor,
        IOptions<TokenRefreshSettings> tokenRefreshSettings) : ViewComponent
    {
        private readonly TokenRefreshSettings _settings = tokenRefreshSettings.Value;

        /// <summary>
        /// Warning window in minutes - show the overlay this many minutes before logout.
        /// IMPORTANT: This must be LESS than InactivityThresholdMinutes, otherwise popup shows immediately!
        /// For production (30 min threshold), use 5. For testing (2 min threshold), use 1.
        /// </summary>
        private const int WarningWindowMinutes = 5;

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var model = new SessionTimeoutViewModel();
            var context = httpContextAccessor.HttpContext;

            if (context?.User?.Identity?.IsAuthenticated != true)
            {
                return View(model);
            }

            // Calculate time until idle timeout forces logout
            var timeSinceActivity = activityTracker.GetTimeSinceLastActivity(context);
            TimeSpan? timeUntilIdleLogout = null;

            if (timeSinceActivity.HasValue)
            {
                var inactivityThreshold = TimeSpan.FromMinutes(_settings.InactivityThresholdMinutes);
                timeUntilIdleLogout = inactivityThreshold - timeSinceActivity.Value;
            }

            // Also check token expiry as a fallback
            var state = await tokenStateManager.GetCurrentTokenStateAsync();
            TimeSpan? timeUntilTokenLogout = null;

            if (state.IsAuthenticated && state.ExternalIdpToken.ExpiryTime.HasValue)
            {
                var expiryUtc = state.ExternalIdpToken.ExpiryTime.Value;
                timeUntilTokenLogout = expiryUtc - DateTime.UtcNow - TimeSpan.FromMinutes(_settings.ForceLogoutAtMinutesRemaining);
            }

            // Use the smaller of the two timeouts (whichever comes first)
            TimeSpan? timeUntilForceLogout = null;
            if (timeUntilIdleLogout.HasValue && timeUntilTokenLogout.HasValue)
            {
                timeUntilForceLogout = timeUntilIdleLogout.Value < timeUntilTokenLogout.Value
                    ? timeUntilIdleLogout.Value
                    : timeUntilTokenLogout.Value;
            }
            else
            {
                timeUntilForceLogout = timeUntilIdleLogout ?? timeUntilTokenLogout;
            }

            if (!timeUntilForceLogout.HasValue)
            {
                return View(model);
            }

            // Show warning WarningWindowMinutes before logout
            if (timeUntilForceLogout > TimeSpan.Zero && timeUntilForceLogout <= TimeSpan.FromMinutes(WarningWindowMinutes))
            {
                model.Show = true;
                model.AutoRedirectSeconds = (int)Math.Ceiling(timeUntilForceLogout.Value.TotalSeconds);
                model.DisplayTime = timeUntilForceLogout.Value.TotalMinutes >= 1
                    ? $"{(int)Math.Ceiling(timeUntilForceLogout.Value.TotalMinutes)} minute{(timeUntilForceLogout.Value.TotalMinutes >= 2 ? "s" : string.Empty)}"
                    : $"{model.AutoRedirectSeconds} seconds";
            }
            else if (timeUntilForceLogout > TimeSpan.FromMinutes(WarningWindowMinutes))
            {
                // Not time to show overlay yet. Schedule a page refresh to the same URL
                // exactly when the warning window starts, so the overlay appears without user action.
                var untilOverlay = timeUntilForceLogout.Value - TimeSpan.FromMinutes(WarningWindowMinutes);
                model.PreOverlayRefreshSeconds = (int)Math.Ceiling(untilOverlay.TotalSeconds);
            }

            return View(model);
        }
    }

    /// <summary>
    /// View model for the session timeout banner
    /// </summary>
    public class SessionTimeoutViewModel
    {
        /// <summary>
        /// Whether to show the timeout warning overlay
        /// </summary>
        public bool Show { get; set; }

        /// <summary>
        /// Seconds until automatic redirect to logout
        /// </summary>
        public int AutoRedirectSeconds { get; set; }

        /// <summary>
        /// Human-readable display of time remaining
        /// </summary>
        public string DisplayTime { get; set; } = string.Empty;

        /// <summary>
        /// Seconds until the page should refresh to show the overlay
        /// </summary>
        public int PreOverlayRefreshSeconds { get; set; }
    }
}
