using System.Diagnostics.CodeAnalysis;
using System.Net;
using GovUK.Dfe.FlexForms.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Services;

public class CorrelationIdForwardingHandlerTests
{
    private const string HeaderName = CorrelationIdForwardingHandler.HeaderName;

    [Fact]
    public async Task SendAsync_ShouldForwardIncomingCorrelationId()
    {
        var incoming = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.Request.Headers[HeaderName] = incoming.ToString();

        var outbound = await SendAsync(context);

        Assert.Equal(incoming.ToString(), SingleHeader(outbound, HeaderName));
    }

    [Fact]
    public async Task SendAsync_ShouldGenerateCorrelationIdAndWriteItBack_WhenIncomingHeaderMissing()
    {
        var context = new DefaultHttpContext();

        var outbound = await SendAsync(context);

        var generated = SingleHeader(outbound, HeaderName);
        Assert.True(Guid.TryParse(generated, out var parsed) && parsed != Guid.Empty);
        Assert.Equal(generated, context.Request.Headers[HeaderName].ToString());
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task SendAsync_ShouldGenerateCorrelationId_WhenIncomingHeaderIsUnusable(string incoming)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[HeaderName] = incoming;

        var outbound = await SendAsync(context);

        var generated = SingleHeader(outbound, HeaderName);
        Assert.NotEqual(incoming, generated);
        Assert.True(Guid.TryParse(generated, out var parsed) && parsed != Guid.Empty);
    }

    [Fact]
    public async Task SendAsync_ShouldGenerateCorrelationId_WhenThereIsNoHttpContext()
    {
        var outbound = await SendAsync(httpContext: null);

        Assert.True(Guid.TryParse(SingleHeader(outbound, HeaderName), out _));
    }

    [Fact]
    public async Task SendAsync_ShouldReplaceCorrelationIdAlreadyOnTheOutboundRequest()
    {
        var incoming = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.Request.Headers[HeaderName] = incoming.ToString();

        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test/");
        request.Headers.TryAddWithoutValidation(HeaderName, "stale-value");

        var outbound = await SendAsync(context, request);

        Assert.Equal(incoming.ToString(), SingleHeader(outbound, HeaderName));
    }

    [Theory]
    [InlineData("X-Template-Id")]
    [InlineData("X-Application-Reference")]
    public async Task SendAsync_ShouldForwardTelemetryHeader_FromTheIncomingRequest(string headerName)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[headerName] = "from-request";

        var outbound = await SendAsync(context);

        Assert.Equal("from-request", SingleHeader(outbound, headerName));
    }

    [Theory]
    [InlineData("X-Template-Id", "TemplateId")]
    [InlineData("X-Application-Reference", "ApplicationReference")]
    public async Task SendAsync_ShouldForwardTelemetryHeader_FromSession_WhenNotOnTheIncomingRequest(
        string headerName,
        string sessionKey)
    {
        var session = new InMemorySession();
        session.SetString(sessionKey, "from-session");
        var context = new DefaultHttpContext();
        context.Features.Set<ISessionFeature>(new StubSessionFeature { Session = session });

        var outbound = await SendAsync(context);

        Assert.Equal("from-session", SingleHeader(outbound, headerName));
    }

    [Fact]
    public async Task SendAsync_ShouldPreferTheIncomingHeader_OverTheSessionValue()
    {
        var session = new InMemorySession();
        session.SetString("TemplateId", "from-session");
        var context = new DefaultHttpContext();
        context.Features.Set<ISessionFeature>(new StubSessionFeature { Session = session });
        context.Request.Headers["X-Template-Id"] = "from-request";

        var outbound = await SendAsync(context);

        Assert.Equal("from-request", SingleHeader(outbound, "X-Template-Id"));
    }

    [Fact]
    public async Task SendAsync_ShouldSkipTelemetryHeaders_WhenSessionIsUnavailable()
    {
        // Tenant bootstrap calls run before UseSession(), so there is no session feature.
        var outbound = await SendAsync(new DefaultHttpContext());

        Assert.False(outbound.Headers.Contains("X-Template-Id"));
        Assert.False(outbound.Headers.Contains("X-Application-Reference"));
    }

    [Fact]
    public async Task SendAsync_ShouldSkipTelemetryHeader_WhenTheSessionValueIsBlank()
    {
        var session = new InMemorySession();
        session.SetString("TemplateId", "   ");
        var context = new DefaultHttpContext();
        context.Features.Set<ISessionFeature>(new StubSessionFeature { Session = session });

        var outbound = await SendAsync(context);

        Assert.False(outbound.Headers.Contains("X-Template-Id"));
    }

    [Fact]
    public async Task SendAsync_ShouldReplaceTelemetryHeaderAlreadyOnTheOutboundRequest()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Template-Id"] = "from-request";

        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test/");
        request.Headers.TryAddWithoutValidation("X-Template-Id", "stale-value");

        var outbound = await SendAsync(context, request);

        Assert.Equal("from-request", SingleHeader(outbound, "X-Template-Id"));
    }

    private static async Task<HttpRequestMessage> SendAsync(
        HttpContext? httpContext,
        HttpRequestMessage? request = null)
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        var inner = new RecordingHandler();
        using var handler = new CorrelationIdForwardingHandler(accessor) { InnerHandler = inner };
        using var invoker = new HttpMessageInvoker(handler);

        request ??= new HttpRequestMessage(HttpMethod.Get, "https://api.test/");
        using var response = await invoker.SendAsync(request, CancellationToken.None);

        return inner.Request!;
    }

    private static string SingleHeader(HttpRequestMessage request, string name)
        => Assert.Single(request.Headers.GetValues(name));

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class StubSessionFeature : ISessionFeature
    {
        public ISession Session { get; set; } = new InMemorySession();
    }

    private sealed class InMemorySession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = [];

        public bool IsAvailable => true;

        public string Id => "test-session";

        public IEnumerable<string> Keys => _store.Keys;

        public void Clear() => _store.Clear();

        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Remove(string key) => _store.Remove(key);

        public void Set(string key, byte[] value) => _store[key] = value;

        public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value)
            => _store.TryGetValue(key, out value);
    }
}
