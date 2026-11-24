using System.Text.Json;
using GovUK.Dfe.FlexForms.Web.Configuration;
using GovUK.Dfe.FlexForms.Web.Middleware;
using GovUK.Dfe.FlexForms.Web.Services.Platform;
using GovUK.Dfe.FlexForms.Web.Services.Tenant;
using GovUK.Dfe.FlexForms.Web.Telemetry;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Middleware;

public sealed class TenantConfigurationMiddlewareTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string PublicHost = "apply.example.gov.uk";

    private readonly ITenantIdResolver _resolver = Substitute.For<ITenantIdResolver>();
    private readonly ITenantConfigurationCache _cache = Substitute.For<ITenantConfigurationCache>();
    private readonly TenantRequestContext _tenantContext = new();
    private bool _nextCalled;
    private Guid? _tenantSeenByNext;

    [Fact]
    public async Task BootstrapDisabled_CarriesOnWithoutResolvingATenant()
    {
        var context = CreateContext();

        await InvokeAsync(context, bootstrapEnabled: false);

        Assert.True(_nextCalled);
        await _resolver.DidNotReceiveWithAnyArgs().ResolveTenantIdAsync(default!, default);
    }

    [Theory]
    [InlineData("/css/site.css")]
    [InlineData("/js/notifications.js")]
    [InlineData("/assets/images/crest.png")]
    [InlineData("/favicon.ico")]
    [InlineData("/health")]
    [InlineData("/HEALTHZ")]
    [InlineData("/liveness")]
    [InlineData("/readiness")]
    [InlineData("/Error/Forbidden")]
    public async Task BypassedPath_CarriesOnWithoutResolvingATenant(string path)
    {
        var context = CreateContext(path: path);

        await InvokeAsync(context);

        Assert.True(_nextCalled);
        await _resolver.DidNotReceiveWithAnyArgs().ResolveTenantIdAsync(default!, default);
    }

    [Fact]
    public async Task HealthSubPath_IsNotBypassed()
    {
        var context = CreateContext(path: "/health/details");
        _resolver.ResolveTenantIdAsync(context, Arg.Any<CancellationToken>()).Returns((Guid?)null);

        await InvokeAsync(context);

        Assert.False(_nextCalled);
        await _resolver.Received(1).ResolveTenantIdAsync(context, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TenantResolved_LoadsItsConfigurationBeforeCarryingOn()
    {
        var context = CreateContext();
        _resolver.ResolveTenantIdAsync(context, Arg.Any<CancellationToken>()).Returns(TenantId);
        LoadReturns(new Dictionary<string, string?> { ["Template:Name"] = "Apply", ["Feature:On"] = "true" });

        await InvokeAsync(context);

        Assert.True(_nextCalled);
        Assert.Equal(TenantId, _tenantSeenByNext);
        Assert.Equal("Test tenant", _tenantContext.TenantName);
        Assert.Equal("Apply", _tenantContext.TenantConfiguration?["Template:Name"]);
        Assert.Equal(TenantId, ReportingTelemetryItems.GetTenantId(context));
    }

    [Fact]
    public async Task NoTenantForAPublicHost_Returns400()
    {
        var context = CreateContext();
        _resolver.ResolveTenantIdAsync(context, Arg.Any<CancellationToken>()).Returns((Guid?)null);

        await InvokeAsync(context);

        Assert.False(_nextCalled);
        await AssertErrorAsync(context, "Could not resolve tenant from request.");
    }

    [Theory]
    [InlineData("10.0.0.4")]
    [InlineData("flexforms-web.azurecontainerapps.io")]
    public async Task NoTenantForAPrivateOrPlatformHost_Returns404WithNoBody(string host)
    {
        var context = CreateContext(host: host);
        _resolver.ResolveTenantIdAsync(context, Arg.Any<CancellationToken>()).Returns((Guid?)null);

        await InvokeAsync(context);

        Assert.False(_nextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Equal(string.Empty, await ReadBodyAsync(context));
    }

    [Fact]
    public async Task ConflictingHostnames_Returns400()
    {
        var context = CreateContext();
        _resolver.ResolveTenantIdAsync(context, Arg.Any<CancellationToken>())
            .ThrowsAsync(new TenantHostnameConflictException("Two hosts"));

        await InvokeAsync(context);

        Assert.False(_nextCalled);
        await AssertErrorAsync(context, "Could not resolve tenant from request.");
    }

    [Fact]
    public async Task PlatformApiFails_Returns400()
    {
        var context = CreateContext();
        _resolver.ResolveTenantIdAsync(context, Arg.Any<CancellationToken>()).Returns(TenantId);
        _cache.GetOrAddAsync(TenantId, Arg.Any<Func<CancellationToken, Task<PlatformTenantConfigurationResponse>>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("503"));

        await InvokeAsync(context);

        Assert.False(_nextCalled);
        Assert.Null(_tenantContext.TenantId);
        await AssertErrorAsync(context, "Failed to load tenant configuration from platform API.");
    }

    [Fact]
    public async Task ClientAbortsTheRequest_StopsWithoutWritingAResponse()
    {
        var context = CreateContext();
        using var aborted = new CancellationTokenSource();
        aborted.Cancel();
        context.RequestAborted = aborted.Token;
        _resolver.ResolveTenantIdAsync(context, Arg.Any<CancellationToken>()).ThrowsAsync(new OperationCanceledException());

        await InvokeAsync(context);

        Assert.False(_nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(string.Empty, await ReadBodyAsync(context));
    }

    [Fact]
    public async Task CanceledWhileTheRequestIsLive_IsNotCaught()
    {
        var context = CreateContext();
        _resolver.ResolveTenantIdAsync(context, Arg.Any<CancellationToken>()).ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => InvokeAsync(context));
        Assert.False(_nextCalled);
    }

    private void LoadReturns(IReadOnlyDictionary<string, string?> configuration) =>
        _cache.GetOrAddAsync(TenantId, Arg.Any<Func<CancellationToken, Task<PlatformTenantConfigurationResponse>>>(), Arg.Any<CancellationToken>())
            .Returns(new PlatformTenantConfigurationResponse(TenantId, "Test tenant", "Web", DateTime.UtcNow, configuration));

    private async Task InvokeAsync(HttpContext context, bool bootstrapEnabled = true)
    {
        var middleware = new TenantConfigurationMiddleware(
            _ =>
            {
                _nextCalled = true;
                _tenantSeenByNext = _tenantContext.TenantId;
                return Task.CompletedTask;
            },
            Options.Create(new PlatformBootstrapOptions { Enabled = bootstrapEnabled }),
            NullLogger<TenantConfigurationMiddleware>.Instance);

        // The loader goes through the cache; the API client behind it is never called in these tests.
        var apiClient = new PlatformConfigurationApiClient(
            new HttpClient(),
            Substitute.For<IPlatformAccessTokenProvider>(),
            Options.Create(new PlatformBootstrapOptions()),
            NullLogger<PlatformConfigurationApiClient>.Instance);

        await middleware.InvokeAsync(context, _tenantContext, _resolver, new TenantConfigurationLoader(apiClient, _cache));
    }

    private static DefaultHttpContext CreateContext(string path = "/applications/dashboard", string host = PublicHost)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Host = new HostString(host);
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task AssertErrorAsync(HttpContext context, string message)
    {
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);
        using var body = JsonDocument.Parse(await ReadBodyAsync(context));
        Assert.Equal(message, body.RootElement.GetProperty("error").GetString());
    }

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}
