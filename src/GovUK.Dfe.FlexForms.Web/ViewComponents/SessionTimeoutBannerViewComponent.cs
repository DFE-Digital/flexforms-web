using System;
using System.Threading.Tasks;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.ViewComponents;

/// <summary>
/// Renders the inactivity warning overlay and client-side idle timers.
/// Timing is driven in the browser (activity events) so the warning is reliable
/// without depending on HTTP Refresh headers or server activity timestamps that
/// are reset on every navigation.
/// </summary>
public class SessionTimeoutBannerViewComponent(
    IHttpContextAccessor httpContextAccessor,
    IOptions<TokenRefreshSettings> tokenRefreshSettings) : ViewComponent
{
    /// <summary>
    /// Show the warning this many minutes before idle logout.
    /// Must be less than <see cref="TokenRefreshSettings.InactivityThresholdMinutes"/>.
    /// </summary>
    private const int WarningWindowMinutes = 5;

    public IViewComponentResult Invoke()
    {
        var model = new SessionTimeoutViewModel();
        var context = httpContextAccessor.HttpContext;

        if (context?.User?.Identity?.IsAuthenticated != true)
            return View(model);

        var settings = tokenRefreshSettings.Value;
        var inactivityMinutes = Math.Max(1, settings.InactivityThresholdMinutes);
        var warningMinutes = Math.Min(WarningWindowMinutes, Math.Max(1, inactivityMinutes - 1));

        model.Enabled = true;
        model.InactivityThresholdSeconds = inactivityMinutes * 60;
        model.WarningWindowSeconds = warningMinutes * 60;
        model.StaySignedInUrl = "/session/stay-signed-in";
        model.TimeoutSignOutUrl = "/session/timeout-sign-out";
        model.ManualSignOutUrl = "/session/sign-out";

        return View(model);
    }
}

/// <summary>
/// View model for the session timeout banner.
/// </summary>
public class SessionTimeoutViewModel
{
    /// <summary>When true, emit the client-side idle/warning script for authenticated users.</summary>
    public bool Enabled { get; set; }

    /// <summary>Total idle seconds before forced sign-out.</summary>
    public int InactivityThresholdSeconds { get; set; }

    /// <summary>Seconds before idle logout at which the warning overlay appears.</summary>
    public int WarningWindowSeconds { get; set; }

    public string StaySignedInUrl { get; set; } = "/session/stay-signed-in";

    public string TimeoutSignOutUrl { get; set; } = "/session/timeout-sign-out";

    public string ManualSignOutUrl { get; set; } = "/session/sign-out";
}
