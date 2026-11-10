using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using GovUK.Dfe.CoreLibs.Http.Interfaces;
using GovUK.Dfe.CoreLibs.Http.Middlewares.RequestTelemetry;
using GovUK.Dfe.FlexForms.Web.Middleware;
using GovUK.Dfe.FlexForms.Web.Telemetry;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Middleware;

public class RequestTelemetryEnrichmentMiddlewareTests
{
    private const string SessionCookieName = ".AspNetCore.Session";

    private const string ReportingKey = "0123456789abcdef0123456789abcdef-test-key";
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly FlexFormsRequestScope _scope = new();
    private readonly RequestTelemetryContext _telemetry = new();

    private async Task<bool> Invoke(HttpContext context, Guid? tenantId = null, string? reportingKey = ReportingKey)
    {
        var nextCalled = false;
        var middleware = new RequestTelemetryEnrichmentMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            Options.Create(new SessionOptions()),
            new ReportingUserIdGenerator(reportingKey),
            NullLogger<RequestTelemetryEnrichmentMiddleware>.Instance);

        var tenantContext = Substitute.For<ITenantRequestContext>();
        tenantContext.TenantId.Returns(tenantId);

        await middleware.InvokeAsync(
            context,
            tenantContext,
            _telemetry,
            _scope,
            Substitute.For<ICorrelationContext>());

        return nextCalled;
    }

    private static DefaultHttpContext SignedIn() => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "dsi-user-1"),
                new Claim(ClaimTypes.Email, "ada.lovelace@education.gov.uk")
            ],
            authenticationType: "Cookies"))
    };

    [Fact]
    public async Task InvokeAsync_ShouldLogThePseudonymousReportingId_NotTheRawUserId()
    {
        await Invoke(SignedIn(), TenantId);

        Assert.Equal(new ReportingUserIdGenerator(ReportingKey).Create(TenantId, "dsi-user-1"), _telemetry.UserId);
        Assert.Equal(PiiMasking.MaskEmail("ada.lovelace@education.gov.uk"), _telemetry.UserEmail);
        Assert.DoesNotContain(_telemetry.ToScopeDictionary().Values, v => v.ToString()!.Contains("dsi-user-1"));
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, ReportingKey)]
    public async Task InvokeAsync_ShouldLogNoUserId_WithoutTenantOrKey(bool withTenant, string? reportingKey)
    {
        await Invoke(SignedIn(), withTenant ? TenantId : null, reportingKey);

        Assert.Null(_telemetry.UserId);
        Assert.NotNull(_telemetry.UserEmail);
    }

    private static DefaultHttpContext Request(ISession session, bool withSessionCookie)
    {
        var context = new DefaultHttpContext { Session = session };
        if (withSessionCookie)
            context.Request.Headers.Cookie = $"{SessionCookieName}=abc";
        return context;
    }

    [Fact]
    public async Task InvokeAsync_ShouldNotReadTheSession_WhenThereIsNoSessionCookie()
    {
        var session = new UnavailableSession();

        var nextCalled = await Invoke(Request(session, withSessionCookie: false));

        Assert.True(nextCalled);
        Assert.Equal(0, session.Reads);
    }

    [Fact]
    public async Task InvokeAsync_ShouldCopyTemplateAndApplicationFromTheSession_WhenThereIsASessionCookie()
    {
        var session = new InMemorySession();
        session.SetString("TemplateId", "template-1");
        session.SetString("ApplicationReference", "LSP-20261102-004");

        var nextCalled = await Invoke(Request(session, withSessionCookie: true));

        Assert.True(nextCalled);
        Assert.Equal("template-1", _scope.TemplateId);
        Assert.Equal("LSP-20261102-004", _scope.ApplicationReference);
    }

    [Fact]
    public async Task InvokeAsync_ShouldBindAnOpaqueReportingSessionId_OnlyWhenThereIsASessionCookie()
    {
        var withCookie = Request(new InMemorySession(), withSessionCookie: true);
        var withoutCookie = Request(new InMemorySession(), withSessionCookie: false);

        await Invoke(withCookie);
        await Invoke(withoutCookie);

        var sessionId = ReportingTelemetryItems.GetSessionId(withCookie);
        Assert.NotNull(sessionId);
        Assert.NotEqual("session-1", sessionId);
        Assert.Null(ReportingTelemetryItems.GetSessionId(withoutCookie));
    }

    private sealed class UnavailableSession : ISession
    {
        public int Reads { get; private set; }

        public bool IsAvailable => false;
        public string Id => string.Empty;
        public IEnumerable<string> Keys => [];

        public Task LoadAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("Redis is down");
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value)
        {
            Reads++;
            throw new InvalidOperationException("Redis is down");
        }

        public void Set(string key, byte[] value) { }
        public void Remove(string key) { }
        public void Clear() { }
    }

    private sealed class InMemorySession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = [];

        public bool IsAvailable => true;
        public string Id => "session-1";
        public IEnumerable<string> Keys => _values.Keys;

        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value) => _values.TryGetValue(key, out value);
        public void Set(string key, byte[] value) => _values[key] = value;
        public void Remove(string key) => _values.Remove(key);
        public void Clear() => _values.Clear();
    }
}
