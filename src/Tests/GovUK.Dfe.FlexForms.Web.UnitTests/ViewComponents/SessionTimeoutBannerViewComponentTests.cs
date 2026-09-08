using System.Security.Claims;
using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Web.ViewComponents;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.ViewComponents;

public class SessionTimeoutBannerViewComponentTests
{
    [Fact]
    public void Invoke_ShouldReturnDisabled_WhenAnonymous()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(new DefaultHttpContext());
        var component = new SessionTimeoutBannerViewComponent(
            accessor,
            Options.Create(new TokenRefreshSettings()));

        var result = Assert.IsType<ViewViewComponentResult>(component.Invoke());
        var model = Assert.IsType<SessionTimeoutViewModel>(result.ViewData!.Model);
        Assert.False(model.Enabled);
    }

    [Fact]
    public void Invoke_ShouldEnableTimers_WhenAuthenticated()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "ada")],
                authenticationType: "test"))
        };
        accessor.HttpContext.Returns(http);
        var component = new SessionTimeoutBannerViewComponent(
            accessor,
            Options.Create(new TokenRefreshSettings { InactivityThresholdMinutes = 20 }));

        var result = Assert.IsType<ViewViewComponentResult>(component.Invoke());
        var model = Assert.IsType<SessionTimeoutViewModel>(result.ViewData!.Model);
        Assert.True(model.Enabled);
        Assert.Equal(20 * 60, model.InactivityThresholdSeconds);
        Assert.Equal(5 * 60, model.WarningWindowSeconds);
        Assert.Equal("/session/stay-signed-in", model.StaySignedInUrl);
    }
}
