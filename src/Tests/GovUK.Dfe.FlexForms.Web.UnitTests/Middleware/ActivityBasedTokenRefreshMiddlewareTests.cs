using System.Security.Claims;
using GovUK.Dfe.CoreLibs.Security.Configurations;
using GovUK.Dfe.FlexForms.Api.Client.Security;
using GovUK.Dfe.FlexForms.Web.Middleware;
using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Web.Telemetry;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Middleware;

public sealed class ActivityBasedTokenRefreshMiddlewareTests
{
    private readonly TokenRefreshSettings _settings = new()
    {
        AbsoluteTimeoutHours = 8,
        InactivityThresholdMinutes = 30,
        ForceLogoutAtMinutesRemaining = 5
    };

    private readonly IUserActivityTracker _tracker = Substitute.For<IUserActivityTracker>();
    private readonly IAuthenticationSchemeStrategy _strategy = Substitute.For<IAuthenticationSchemeStrategy>();
    private bool _nextCalled;

    [Fact]
    public async Task SignedOutRequest_CarriesOnWithoutSessionChecks()
    {
        var context = CreateContext(new ClaimsPrincipal(new ClaimsIdentity()));

        await InvokeAsync(context);

        Assert.True(_nextCalled);
        Assert.Empty(_tracker.ReceivedCalls());
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/HEALTHZ")]
    [InlineData("/signin-oidc")]
    [InlineData("/session/timeout-sign-out")]
    [InlineData("/Logout")]
    [InlineData("/js/site.js")]
    [InlineData("/applications/logo.svg")]
    [InlineData("/applications/font.woff2")]
    public async Task SkippedPath_CarriesOnWithoutSessionChecks(string path)
    {
        var context = CreateContext(SignedIn(), path);

        await InvokeAsync(context);

        Assert.True(_nextCalled);
        Assert.Empty(_tracker.ReceivedCalls());
    }

    [Fact]
    public async Task TestAuthenticationActive_CarriesOnWithoutSessionChecks()
    {
        var context = CreateContext(SignedIn(), environment: "Development");

        await InvokeAsync(context, testAuthenticationEnabled: true);

        Assert.True(_nextCalled);
        Assert.Empty(_tracker.ReceivedCalls());
    }

    [Fact]
    public async Task TestAuthenticationEnabledInProduction_StillRunsSessionChecks()
    {
        var context = CreateContext(SignedIn(), environment: "Production");
        _tracker.HasSessionExpired(context, _settings.AbsoluteTimeoutHours).Returns(true);

        await InvokeAsync(context, testAuthenticationEnabled: true);

        AssertSignedOut(context, "session_expired");
    }

    [Fact]
    public async Task NoActivityTrackerOrStrategyRegistered_CarriesOn()
    {
        var context = CreateContext(SignedIn(), registerSessionServices: false);

        await InvokeAsync(context);

        Assert.True(_nextCalled);
    }

    [Fact]
    public async Task SessionPastTheAbsoluteTimeout_IsSignedOut()
    {
        var context = CreateContext(SignedIn());
        _tracker.HasSessionExpired(context, _settings.AbsoluteTimeoutHours).Returns(true);

        await InvokeAsync(context);

        AssertSignedOut(context, "session_expired");
        _tracker.DidNotReceive().RecordActivity(Arg.Any<HttpContext>());
    }

    [Fact]
    public async Task IdleUser_IsSignedOut()
    {
        var context = CreateContext(SignedIn());
        _tracker.IsUserInactive(context, _settings.InactivityThresholdMinutes).Returns(true);

        await InvokeAsync(context);

        AssertSignedOut(context, "idle_timeout");
        _tracker.DidNotReceive().RecordActivity(Arg.Any<HttpContext>());
    }

    [Fact]
    public async Task ActiveUserWithNoRefreshDue_RecordsActivityAndCarriesOn()
    {
        var context = CreateContext(SignedIn());
        _strategy.CanRefreshTokenAsync(context).Returns(false);

        await InvokeAsync(context);

        Assert.True(_nextCalled);
        _tracker.Received(1).RecordActivity(context);
        await _strategy.DidNotReceive().RefreshExternalIdpTokenAsync(Arg.Any<HttpContext>());
    }

    [Fact]
    public async Task RefreshDueAndSucceeds_RecordsActivityAndCarriesOn()
    {
        var context = CreateContext(SignedIn());
        _strategy.CanRefreshTokenAsync(context).Returns(true);
        _strategy.RefreshExternalIdpTokenAsync(context).Returns(true);

        await InvokeAsync(context);

        Assert.True(_nextCalled);
        _tracker.Received(1).RecordActivity(context);
    }

    [Fact]
    public async Task RefreshFailsAndTokenIsAboutToExpire_IsSignedOut()
    {
        var context = CreateContext(SignedIn());
        RefreshFails(context, new TokenInfo { Value = "token", ExpiryTime = DateTime.UtcNow.AddMinutes(4) });

        await InvokeAsync(context);

        AssertSignedOut(context, "token_expiring");
        _tracker.DidNotReceive().RecordActivity(Arg.Any<HttpContext>());
    }

    [Fact]
    public async Task RefreshFailsButTokenHasTimeLeft_CarriesOn()
    {
        var context = CreateContext(SignedIn());
        RefreshFails(context, new TokenInfo { Value = "token", ExpiryTime = DateTime.UtcNow.AddMinutes(20) });

        await InvokeAsync(context);

        Assert.True(_nextCalled);
        _tracker.Received(1).RecordActivity(context);
    }

    [Fact]
    public async Task RefreshFailsAndThereIsNoToken_CarriesOn()
    {
        var context = CreateContext(SignedIn());
        RefreshFails(context, new TokenInfo());

        await InvokeAsync(context);

        Assert.True(_nextCalled);
    }

    [Fact]
    public async Task RefreshFailsAndReadingTheTokenThrows_CarriesOn()
    {
        var context = CreateContext(SignedIn());
        _strategy.CanRefreshTokenAsync(context).Returns(true);
        _strategy.RefreshExternalIdpTokenAsync(context).Returns(false);
        _strategy.GetExternalIdpTokenAsync(context).ThrowsAsync(new InvalidOperationException("boom"));

        await InvokeAsync(context);

        Assert.True(_nextCalled);
    }

    [Fact]
    public async Task RefreshThrows_CarriesOn()
    {
        var context = CreateContext(SignedIn());
        _strategy.CanRefreshTokenAsync(context).ThrowsAsync(new InvalidOperationException("boom"));

        await InvokeAsync(context);

        Assert.True(_nextCalled);
        _tracker.Received(1).RecordActivity(context);
    }

    [Fact]
    public async Task ActivityTrackerThrows_CarriesOn()
    {
        var context = CreateContext(SignedIn());
        _tracker.HasSessionExpired(Arg.Any<HttpContext>(), Arg.Any<int>()).Throws(new InvalidOperationException("boom"));

        await InvokeAsync(context);

        Assert.True(_nextCalled);
    }

    private void RefreshFails(HttpContext context, TokenInfo token)
    {
        _strategy.CanRefreshTokenAsync(context).Returns(true);
        _strategy.RefreshExternalIdpTokenAsync(context).Returns(false);
        _strategy.GetExternalIdpTokenAsync(context).Returns(token);
    }

    private void AssertSignedOut(HttpContext context, string reason)
    {
        Assert.False(_nextCalled);
        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal($"/session/timeout-sign-out?reason={reason}", context.Response.Headers.Location.ToString());
    }

    private async Task InvokeAsync(HttpContext context, bool testAuthenticationEnabled = false)
    {
        var middleware = new ActivityBasedTokenRefreshMiddleware(
            _ =>
            {
                _nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<ActivityBasedTokenRefreshMiddleware>.Instance,
            Options.Create(_settings),
            Options.Create(new TestAuthenticationOptions { Enabled = testAuthenticationEnabled }),
            Options.Create(new EntraSsoOptions()),
            new ReportingUserIdGenerator(null));

        await middleware.InvokeAsync(context);
    }

    private DefaultHttpContext CreateContext(
        ClaimsPrincipal user,
        string path = "/applications/dashboard",
        string environment = "Production",
        bool registerSessionServices = true)
    {
        var hostEnvironment = Substitute.For<IWebHostEnvironment>();
        hostEnvironment.EnvironmentName.Returns(environment);

        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(hostEnvironment);
        if (registerSessionServices)
        {
            services.AddSingleton(_tracker);
            services.AddSingleton(_strategy);
        }

        var context = new DefaultHttpContext { User = user, RequestServices = services.BuildServiceProvider() };
        context.Request.Path = path;
        return context;
    }

    private static ClaimsPrincipal SignedIn() =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Cookies"));
}
