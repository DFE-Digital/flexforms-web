using System.Net;
using System.Text;
using System.Text.Json;
using GovUK.Dfe.FlexForms.Web.Configuration;
using GovUK.Dfe.FlexForms.Web.Services.Platform;
using GovUK.Dfe.FlexForms.Web.Services.Tenant;
using Microsoft.AspNetCore.Http;
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

    private static TenantIdResolver CreateResolver(HttpMessageHandler handler, TenantHostnameCache cache)
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

        return new TenantIdResolver(
            apiClient,
            cache,
            options,
            NullLogger<TenantIdResolver>.Instance);
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
