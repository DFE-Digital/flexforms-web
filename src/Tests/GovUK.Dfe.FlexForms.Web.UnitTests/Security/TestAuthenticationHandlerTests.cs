using System.Security.Claims;
using System.Text.Encodings.Web;
using GovUK.Dfe.FlexForms.Web.Authentication;
using GovUK.Dfe.FlexForms.Web.Middleware;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using GovUK.Dfe.FlexForms.Web.UnitTests.Telemetry;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Security;

public sealed class TestAuthenticationHandlerTests
{
    private static readonly Guid HostTenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherTenant = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private const string Email = "tester@example.com";

    private readonly FakeSession _session = new("session-1");

    [Fact]
    public async Task NoTestSession_ReturnsNoResult()
    {
        var context = CreateContext(HostTenant);

        var result = await AuthenticateAsync(context);

        Assert.True(result.None);
    }

    [Fact]
    public async Task EmailWithoutToken_ReturnsNoResult()
    {
        _session.SetString("TestAuth:Email", Email);
        var context = CreateContext(HostTenant);

        var result = await AuthenticateAsync(context);

        Assert.True(result.None);
    }

    [Fact]
    public async Task SessionForTheHostTenant_SignsInWithTheTenantClaimAndTokens()
    {
        StartSession(HostTenant.ToString());
        var context = CreateContext(HostTenant);

        var result = await AuthenticateAsync(context);

        Assert.True(result.Succeeded);
        var principal = result.Principal!;
        Assert.Equal(TestAuthenticationHandler.SchemeName, principal.Identity?.AuthenticationType);
        Assert.Equal(Email, principal.FindFirstValue(ClaimTypes.Email));
        Assert.Equal(Email, principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal(HostTenant.ToString(), principal.FindFirstValue(TenantSessionBindingMiddleware.TenantIdClaimType));
        Assert.Equal("test-token", result.Properties!.GetTokenValue("id_token"));
        Assert.Equal("test-token", result.Properties.GetTokenValue("access_token"));
    }

    [Fact]
    public async Task SessionForAnotherTenant_FailsAndClearsTheTestSession()
    {
        StartSession(OtherTenant.ToString());
        var context = CreateContext(HostTenant);

        var result = await AuthenticateAsync(context);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
        Assert.Null(_session.GetString("TestAuth:Email"));
        Assert.Null(_session.GetString("TestAuth:Token"));
        Assert.Null(_session.GetString("TestAuth:TenantId"));
    }

    [Fact]
    public async Task NoTenantResolvedForTheHost_SignsIn()
    {
        StartSession(OtherTenant.ToString());
        var context = CreateContext(resolvedTenant: null);

        var result = await AuthenticateAsync(context);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task SessionWithNoValidTenant_SignsInWithoutATenantClaim(string? sessionTenant)
    {
        StartSession(sessionTenant);
        var context = CreateContext(HostTenant);

        var result = await AuthenticateAsync(context);

        Assert.True(result.Succeeded);
        Assert.Null(result.Principal!.FindFirst(TenantSessionBindingMiddleware.TenantIdClaimType));
        Assert.Equal(Email, _session.GetString("TestAuth:Email"));
    }

    [Theory]
    [InlineData(null, "/TestLogin")]
    [InlineData("/applications/dashboard?page=2", "/TestLogin?returnUrl=%2Fapplications%2Fdashboard%3Fpage%3D2")]
    public async Task Challenge_RedirectsToTestLogin(string? redirectUri, string expectedLocation)
    {
        var context = CreateContext(HostTenant);
        var handler = await CreateHandlerAsync(context);

        await handler.ChallengeAsync(new AuthenticationProperties { RedirectUri = redirectUri });

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal(expectedLocation, context.Response.Headers.Location.ToString());
    }

    private void StartSession(string? tenant)
    {
        _session.SetString("TestAuth:Email", Email);
        _session.SetString("TestAuth:Token", "test-token");
        if (tenant is not null)
            _session.SetString("TestAuth:TenantId", tenant);
    }

    private static async Task<AuthenticateResult> AuthenticateAsync(HttpContext context) =>
        await (await CreateHandlerAsync(context)).AuthenticateAsync();

    private static async Task<TestAuthenticationHandler> CreateHandlerAsync(HttpContext context)
    {
        var options = Substitute.For<IOptionsMonitor<TestAuthenticationSchemeOptions>>();
        options.Get(Arg.Any<string>()).Returns(new TestAuthenticationSchemeOptions());

#pragma warning disable CS0618 // The handler still takes ISystemClock.
        var handler = new TestAuthenticationHandler(options, NullLoggerFactory.Instance, UrlEncoder.Default, new SystemClock());
#pragma warning restore CS0618

        await handler.InitializeAsync(
            new AuthenticationScheme(TestAuthenticationHandler.SchemeName, null, typeof(TestAuthenticationHandler)),
            context);
        return handler;
    }

    private DefaultHttpContext CreateContext(Guid? resolvedTenant)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantRequestContext>(new TenantRequestContext { TenantId = resolvedTenant });

        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        context.Features.Set<ISessionFeature>(new SessionFeature { Session = _session });
        context.Request.Path = "/applications/dashboard";
        return context;
    }
}
