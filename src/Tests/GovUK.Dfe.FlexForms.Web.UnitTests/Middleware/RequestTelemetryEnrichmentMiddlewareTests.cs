using System.Diagnostics.CodeAnalysis;
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

    private readonly FlexFormsRequestScope _scope = new();

    private async Task<bool> Invoke(HttpContext context)
    {
        var nextCalled = false;
        var middleware = new RequestTelemetryEnrichmentMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            Options.Create(new SessionOptions()),
            NullLogger<RequestTelemetryEnrichmentMiddleware>.Instance);

        await middleware.InvokeAsync(
            context,
            Substitute.For<ITenantRequestContext>(),
            new RequestTelemetryContext(),
            _scope,
            Substitute.For<ICorrelationContext>());

        return nextCalled;
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
