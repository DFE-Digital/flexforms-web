using GovUK.Dfe.FlexForms.Web.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Extensions;

public class SecurityHeadersExtensionsTests
{
    private const string CspHeader = "Content-Security-Policy";
    private const string CspReportOnlyHeader = "Content-Security-Policy-Report-Only";

    [Fact]
    public async Task UseFlexFormsSecurityHeaders_ShouldAddHardeningHeaders()
    {
        using var response = await SendAsync(Environments.Production);

        Assert.Equal("DENY", Header(response, "X-Frame-Options"));
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("strict-origin-when-cross-origin", Header(response, "Referrer-Policy"));
        var permissions = Header(response, "Permissions-Policy");
        Assert.Contains("camera=()", permissions);
        Assert.Contains("geolocation=()", permissions);
        Assert.Contains("microphone=()", permissions);
    }

    [Fact]
    public async Task UseFlexFormsSecurityHeaders_ShouldEnforceCspByDefault()
    {
        using var response = await SendAsync(Environments.Production);

        Assert.False(response.Headers.Contains(CspReportOnlyHeader));
        var csp = Header(response, CspHeader);
        Assert.Contains("default-src 'self'", csp);
        Assert.Contains("'nonce-", csp);
        Assert.Contains("https://js.monitor.azure.com", csp);
        Assert.Contains("style-src 'self' 'unsafe-inline'", csp);
        Assert.Contains("object-src 'none'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.DoesNotContain("localhost", csp);
    }

    [Fact]
    public async Task UseFlexFormsSecurityHeaders_ShouldUseReportOnly_WhenEnforcementDisabled()
    {
        using var response = await SendAsync(
            Environments.Production,
            new Dictionary<string, string?> { [SecurityHeadersExtensions.EnforceCspConfigKey] = "false" });

        Assert.False(response.Headers.Contains(CspHeader));
        Assert.Contains("default-src 'self'", Header(response, CspReportOnlyHeader));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Local")]
    public async Task UseFlexFormsSecurityHeaders_ShouldAllowLocalDevTooling_InLocalEnvironments(string environmentName)
    {
        using var response = await SendAsync(environmentName);

        var csp = Header(response, CspHeader);
        Assert.Contains("ws://localhost:*", csp);
        Assert.Contains("http://localhost:*", csp);
    }

    [Fact]
    public async Task UseFlexFormsSecurityHeaders_ShouldExposeNonceToRequest()
    {
        string? nonce = null;
        using var response = await SendAsync(
            Environments.Production,
            onRequest: context => nonce = NetEscapades.AspNetCore.SecurityHeaders.HttpContextExtensions.GetNonce(context));

        Assert.False(string.IsNullOrEmpty(nonce));
        Assert.Contains($"'nonce-{nonce}'", Header(response, CspHeader));
    }

    private static string Header(HttpResponseMessage response, string name) =>
        string.Join(", ", response.Headers.GetValues(name));

    private static async Task<HttpResponseMessage> SendAsync(
        string environmentName,
        Dictionary<string, string?>? settings = null,
        Action<HttpContext>? onRequest = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings ?? new Dictionary<string, string?>())
            .Build();
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        using var host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .Configure(app =>
                {
                    app.UseFlexFormsSecurityHeaders(configuration, environment);
                    app.Run(context =>
                    {
                        onRequest?.Invoke(context);
                        return context.Response.WriteAsync("ok");
                    });
                }))
            .StartAsync();

        return await host.GetTestClient().GetAsync("/");
    }
}
