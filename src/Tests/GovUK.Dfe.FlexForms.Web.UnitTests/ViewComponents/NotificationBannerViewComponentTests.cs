using GovUK.Dfe.FlexForms.Application.Options;
using GovUK.Dfe.FlexForms.Web.ViewComponents;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.ViewComponents;

public class NotificationBannerViewComponentTests
{
    [Fact]
    public void Invoke_ShouldReturnEmpty_WhenDisabledOrBlank()
    {
        var disabled = new NotificationBannerViewComponent(Options.Create(new NotificationBannerOptions { Enabled = false, Message = "Hi" }));
        Assert.IsType<ContentViewComponentResult>(disabled.Invoke());

        var blank = new NotificationBannerViewComponent(Options.Create(new NotificationBannerOptions { Enabled = true, Message = " " }));
        Assert.IsType<ContentViewComponentResult>(blank.Invoke());
    }

    [Fact]
    public void Invoke_ShouldReturnView_WhenEnabledWithMessage()
    {
        var options = new NotificationBannerOptions { Enabled = true, Message = "Outage", Heading = "Important" };
        var component = new NotificationBannerViewComponent(Options.Create(options));

        var result = Assert.IsType<ViewViewComponentResult>(component.Invoke());
        Assert.Same(options, result.ViewData!.Model);
    }
}
