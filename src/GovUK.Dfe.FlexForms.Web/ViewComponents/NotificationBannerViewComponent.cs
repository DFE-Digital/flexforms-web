using GovUK.Dfe.FlexForms.Application.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.ViewComponents;

/// <summary>
/// View component that conditionally renders a GOV.UK notification banner across all pages.
/// Controlled by the <see cref="NotificationBannerOptions"/> feature flag in appsettings.
/// </summary>
public class NotificationBannerViewComponent(IOptions<NotificationBannerOptions> options) : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        var settings = options.Value;

        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.Message))
        {
            return Content(string.Empty);
        }

        return View(settings);
    }
}
