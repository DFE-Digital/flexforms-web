using System.Net;
using System.Text;
using System.Text.Json;
using GovUK.Dfe.FlexForms.Web.Configuration;
using GovUK.Dfe.FlexForms.Web.Services.Platform;
using GovUK.Dfe.FlexForms.Web.Services.Tenant;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Services.Tenant;

public class TenantIdResolverCacheTests
{
    [Fact]
    public async Task ResolveTenantIdAsync_UsesSharedCache_AcrossResolverInstances()
    {
        var tenantId = Guid.NewGuid();
        var handler = new StubHandler(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            tenantId,
            tenantName = "Visits",
            hostname = "visits.example.gov.uk"
        }));
        var cache = new TenantHostnameCache();
        var first = CreateResolver(handler, cache);
        var second = CreateResolver(handler, cache);

        var one = await first.ResolveTenantIdAsync(PublicHostContext(), CancellationToken.None);
        var two = await second.ResolveTenantIdAsync(PublicHostContext(), CancellationToken.None);

        Assert.Equal(tenantId, one);
        Assert.Equal(tenantId, two);
        Assert.Equal(1, handler.SendCount);
    }

    [Fact]
    public async Task ResolveTenantIdAsync_IgnoresTenantHeader_WhenPublicHostnameResolves()
    {
        var hostnameTenant = Guid.NewGuid();
        var headerTenant = Guid.NewGuid();
        var handler = new StubHandler(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            tenantId = hostnameTenant,
            tenantName = "Visits",
            hostname = "visits.example.gov.uk"
        }));
        var resolver = CreateResolver(handler, new TenantHostnameCache());
        var context = PublicHostContext();
        context.Request.Headers["X-Tenant-ID"] = headerTenant.ToString();

        var resolved = await resolver.ResolveTenantIdAsync(context, CancellationToken.None);

        Assert.Equal(hostnameTenant, resolved);
    }

    [Fact]
    public async Task ResolveTenantIdAsync_Throws_WhenForwardedAndRequestHostsConflict()
    {
        var resolver = CreateResolver(
            new StubHandler(HttpStatusCode.OK, "{}"),
            new TenantHostnameCache());
        var context = PublicHostContext();
        context.Request.Headers["X-Forwarded-Host"] = "other.example.gov.uk";

        var ex = await Assert.ThrowsAsync<TenantHostnameConflictException>(
            () => resolver.ResolveTenantIdAsync(context, CancellationToken.None));

        Assert.Contains("X-Forwarded-Host", ex.Message);
    }

    [Fact]
    public async Task ResolveTenantIdAsync_UsesHeader_OnLocalhostWhenEnvironmentAllows()
    {
        var hostnameTenant = Guid.NewGuid();
        var headerTenant = Guid.NewGuid();
        var handler = new StubHandler(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            tenantId = hostnameTenant,
            tenantName = "Local",
            hostname = "localhost"
        }));
        var resolver = CreateResolver(handler, new TenantHostnameCache(), "Development");
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("localhost");
        context.Request.Headers["X-Tenant-ID"] = headerTenant.ToString();

        var resolved = await resolver.ResolveTenantIdAsync(context, CancellationToken.None);

        Assert.Equal(headerTenant, resolved);
    }

    [Fact]
    public async Task ResolveTenantIdAsync_IgnoresHeader_OnLocalhostWhenEnvironmentDisallows()
    {
        var hostnameTenant = Guid.NewGuid();
        var headerTenant = Guid.NewGuid();
        var handler = new StubHandler(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            tenantId = hostnameTenant,
            tenantName = "Local",
            hostname = "localhost"
        }));
        var resolver = CreateResolver(handler, new TenantHostnameCache(), "Production");
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("localhost");
        context.Request.Headers["X-Tenant-ID"] = headerTenant.ToString();

        var resolved = await resolver.ResolveTenantIdAsync(context, CancellationToken.None);

        Assert.Equal(hostnameTenant, resolved);
    }

    [Fact]
    public async Task ResolveTenantIdAsync_UsesQueryTenantId_WhenNoHostnameAndEnvironmentAllows()
    {
        var queryTenant = Guid.NewGuid();
        var resolver = CreateResolver(
            new StubHandler(HttpStatusCode.OK, "{}"),
            new TenantHostnameCache(),
            "Development");
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(
            "s184d01-extapp-flexformsweb.agreeabledune-8224bf9e.westeurope.azurecontainerapps.io");
        context.Request.QueryString = new QueryString($"?tenantId={queryTenant}");

        var resolved = await resolver.ResolveTenantIdAsync(context, CancellationToken.None);

        Assert.Equal(queryTenant, resolved);
    }

    [Fact]
    public async Task ResolveTenantIdAsync_IgnoresExplicitTenantId_WhenEnvironmentDisallows()
    {
        var headerTenant = Guid.NewGuid();
        var resolver = CreateResolver(
            new StubHandler(HttpStatusCode.OK, "{}"),
            new TenantHostnameCache(),
            "Production");
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(
            "s184d01-extapp-flexformsweb.agreeabledune-8224bf9e.westeurope.azurecontainerapps.io");
        context.Request.Headers["X-Tenant-ID"] = headerTenant.ToString();

        var resolved = await resolver.ResolveTenantIdAsync(context, CancellationToken.None);

        Assert.Null(resolved);
    }

    [Fact]
    public async Task ResolveTenantIdAsync_UsesStaleCache_WhenPlatformReturns429()
    {
        var tenantId = Guid.NewGuid();
        var cache = new TenantHostnameCache();
        cache.Set("visits.example.gov.uk", tenantId, DateTimeOffset.UtcNow.AddMinutes(-5));

        var handler = new StubHandler(HttpStatusCode.TooManyRequests, "The request is blocked.");
        var resolver = CreateResolver(handler, cache);

        var resolved = await resolver.ResolveTenantIdAsync(PublicHostContext(), CancellationToken.None);

        Assert.Equal(tenantId, resolved);
        Assert.Equal(1, handler.SendCount);
    }

    private static TenantIdResolver CreateResolver(
        HttpMessageHandler handler,
        TenantHostnameCache cache,
        string? environmentName = null)
    {
        var options = Options.Create(new PlatformBootstrapOptions
        {
            ApiBaseUrl = "https://api.example",
            TenantConfigurationCacheMinutes = 10
        });
        var tokens = Substitute.For<IPlatformAccessTokenProvider>();
        tokens.GetAccessTokenAsync(Arg.Any<CancellationToken>()).Returns("token");

        var apiClient = new PlatformConfigurationApiClient(
            new HttpClient(handler),
            tokens,
            options,
            NullLogger<PlatformConfigurationApiClient>.Instance);

        IHostEnvironment? environment = null;
        if (environmentName is not null)
        {
            environment = Substitute.For<IHostEnvironment>();
            environment.EnvironmentName.Returns(environmentName);
        }

        return new TenantIdResolver(
            apiClient,
            cache,
            options,
            NullLogger<TenantIdResolver>.Instance,
            environment);
    }

    private static DefaultHttpContext PublicHostContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("visits.example.gov.uk");
        return context;
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int SendCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            SendCount++;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
