using GovUK.Dfe.FlexForms.Application.Diagnostics;
using GovUK.Dfe.FlexForms.Web.Middleware;
using GovUK.Dfe.FlexForms.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Middleware;

public class RequestTimingMiddlewareTests
{
    private readonly CapturingLogger _logger = new();

    private RequestTimingMiddleware Middleware(RequestDelegate next, params (string Key, string Value)[] settings) =>
        new(next,
            new ConfigurationBuilder().AddInMemoryCollection(settings.ToDictionary(pair => pair.Key, pair => (string?)pair.Value)).Build(),
            _logger);

    private static HttpContext Request(string path) =>
        new DefaultHttpContext { Request = { Method = "POST", Path = path } };

    [Fact]
    public async Task InvokeAsync_ShouldLogTheTotalAndEachStep_ForFormRequests()
    {
        var middleware = Middleware(_ =>
        {
            using (RequestTimings.Measure("save page"))
                RequestTimings.Count("save body KB", 12);
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(Request("/applications/TRF-1/task/page"));

        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Matches(@"^\[TIMING\] POST /applications/TRF-1/task/page took \d+ ms with status 200: ", entry.Message);
        Assert.Contains("save page=", entry.Message);
        Assert.Contains("save body KB=12", entry.Message);
        Assert.Null(RequestTimings.Current);
    }

    [Fact]
    public async Task InvokeAsync_ShouldNotTimeOtherPaths()
    {
        RequestTimings? seen = null;
        var middleware = Middleware(_ =>
        {
            seen = RequestTimings.Current;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(Request("/admin/template-manager"));

        Assert.Null(seen);
        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public async Task InvokeAsync_ShouldWarn_WhenTheRequestIsSlow()
    {
        var middleware = Middleware(async _ => await Task.Delay(20), ("RequestTimings:SlowRequestMs", "10"));

        await middleware.InvokeAsync(Request("/applications/TRF-1"));

        Assert.Equal(LogLevel.Warning, Assert.Single(_logger.Entries).Level);
    }

    [Fact]
    public async Task InvokeAsync_ShouldDoNothing_WhenTurnedOff()
    {
        var middleware = Middleware(_ => Task.CompletedTask, ("RequestTimings:Enabled", "false"));

        await middleware.InvokeAsync(Request("/applications/TRF-1"));

        Assert.Empty(_logger.Entries);
    }

    [Theory]
    [InlineData("https://api.test/v1/applications/reference/TRF-20261026-004", "/v1/applications/reference/{id}")]
    [InlineData("https://api.test/v1/applications/9a4e9c58-9135-468c-b154-7b966f7acfb7/responses", "/v1/applications/{id}/responses")]
    [InlineData("https://api.test/v1/Templates/9a4e9c58-9135-468c-b154-7b966f7acfb7/versions/1.1.110?x=1", "/v1/Templates/{id}/versions/{id}")]
    [InlineData("https://api.test/v1/me/permissions", "/v1/me/permissions")]
    public void StepPath_ShouldReplaceIdsSoCallsToTheSameEndpointAddUp(string url, string expected)
    {
        Assert.Equal(expected, RequestTimingHandler.StepPath(new Uri(url)));
    }

    private sealed class CapturingLogger : ILogger<RequestTimingMiddleware>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
