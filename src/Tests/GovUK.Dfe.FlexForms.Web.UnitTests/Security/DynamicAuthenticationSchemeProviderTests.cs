using GovUK.Dfe.CoreLibs.Security.Configurations;
using GovUK.Dfe.CoreLibs.Security.EntraSso;
using GovUK.Dfe.FlexForms.Web.Authentication;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Security.Claims;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Security;

public class DynamicAuthenticationSchemeProviderTests
{
    [Fact]
    public async Task GetDefaultForbidSchemeAsync_UsesCookie_NotOpenIdConnect()
    {
        var provider = CreateProvider();

        var scheme = await provider.GetDefaultForbidSchemeAsync();

        Assert.NotNull(scheme);
        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, scheme!.Name);
    }

    [Fact]
    public async Task GetDefaultChallengeSchemeAsync_UsesOpenIdConnect()
    {
        var provider = CreateProvider();

        var scheme = await provider.GetDefaultChallengeSchemeAsync();

        Assert.NotNull(scheme);
        Assert.Equal(OpenIdConnectDefaults.AuthenticationScheme, scheme!.Name);
    }

    [Fact]
    public async Task GetDefaultAuthenticateSchemeAsync_IgnoresServiceHeaders_InProduction()
    {
        var provider = CreateProvider("Production", serviceEmail: "svc@education.gov.uk");

        var scheme = await provider.GetDefaultAuthenticateSchemeAsync();

        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, scheme!.Name);
    }

    private static DynamicAuthenticationSchemeProvider CreateProvider(
        string environmentName = "Development",
        string? serviceEmail = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication()
            .AddCookie()
            .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, _ => { })
            .AddScheme<TestAuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.SchemeName, _ => { })
            .AddScheme<InternalServiceAuthenticationSchemeOptions, InternalServiceAuthenticationHandler>(
                InternalServiceAuthenticationHandler.SchemeName, _ => { });

        var httpContext = new DefaultHttpContext();
        if (serviceEmail is not null)
            httpContext.Request.Headers["x-service-email"] = serviceEmail;

        var sp = services.BuildServiceProvider();
        httpContext.RequestServices = sp;

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var authOptions = sp.GetRequiredService<IOptions<AuthenticationOptions>>();

        return new DynamicAuthenticationSchemeProvider(
            authOptions,
            accessor,
            Options.Create(new TestAuthenticationOptions { Enabled = false }),
            Options.Create(new EntraSsoOptions { Enabled = false }),
            CreateEnvironment(environmentName),
            new ConfigurationBuilder().Build());
    }

    private static IHostEnvironment CreateEnvironment(string name)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(name);
        return environment;
    }
}

public class AdminAreaAuthorizationResultHandlerTests
{
    [Theory]
    [InlineData("/admin", true)]
    [InlineData("/admin/", true)]
    [InlineData("/admin/organisation-settings", true)]
    [InlineData("/applications/dashboard", false)]
    [InlineData("/", false)]
    public void IsAdminPath_MatchesAdminRoutes(string path, bool expected)
    {
        Assert.Equal(expected, AdminAreaAuthorizationResultHandler.IsAdminPath(path));
    }

    [Fact]
    public async Task HandleAsync_Returns404_WhenAuthenticatedUserForbiddenOnAdmin()
    {
        var handler = new AdminAreaAuthorizationResultHandler();
        var context = new DefaultHttpContext();
        context.Request.Path = "/admin";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "user")],
            authenticationType: "Cookies"));

        var nextCalled = false;
        await handler.HandleAsync(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            context,
            new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build(),
            PolicyAuthorizationResult.Forbid());

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task HandleAsync_DoesNotForce404_WhenPathIsNotAdmin()
    {
        var handler = new AdminAreaAuthorizationResultHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
        var sp = services.BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = sp };
        context.Request.Path = "/applications/dashboard";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "user")],
            authenticationType: "Cookies"));

        await handler.HandleAsync(
            _ => Task.CompletedTask,
            context,
            new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build(),
            PolicyAuthorizationResult.Forbid());

        Assert.NotEqual(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }
}
