using GovUK.Dfe.FlexForms.Application.Diagnostics;

namespace GovUK.Dfe.FlexForms.Web.Middleware;

/// <summary>
/// Logs one "[TIMING]" line per request under the configured paths, with the total time and each step measured
/// with <see cref="RequestTimings"/>. Requests slower than <c>RequestTimings:SlowRequestMs</c> are logged as warnings.
/// </summary>
/// <remarks>
/// Settings (all optional): <c>RequestTimings:Enabled</c> (default true), <c>RequestTimings:PathPrefixes</c>
/// (default "/applications") and <c>RequestTimings:SlowRequestMs</c> (default 2000).
/// </remarks>
public sealed class RequestTimingMiddleware(
    RequestDelegate next,
    IConfiguration configuration,
    ILogger<RequestTimingMiddleware> logger)
{
    private readonly bool _enabled = configuration.GetValue("RequestTimings:Enabled", true);
    private readonly string[] _pathPrefixes = configuration.GetSection("RequestTimings:PathPrefixes").Get<string[]>() is { Length: > 0 } prefixes
        ? prefixes
        : ["/applications"];
    private readonly double _slowRequestMs = configuration.GetValue("RequestTimings:SlowRequestMs", 2000d);

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_enabled || !ShouldTime(context.Request.Path))
        {
            await next(context);
            return;
        }

        var timings = RequestTimings.Start();
        try
        {
            await next(context);
        }
        finally
        {
            RequestTimings.Stop();
            var totalMs = Math.Round(timings.Elapsed.TotalMilliseconds);
            logger.Log(
                totalMs >= _slowRequestMs ? LogLevel.Warning : LogLevel.Information,
                "[TIMING] {Method} {Path} took {TotalMs} ms with status {StatusCode}: {Breakdown}",
                context.Request.Method,
                context.Request.Path.Value,
                totalMs,
                context.Response.StatusCode,
                timings.Describe());
        }
    }

    private bool ShouldTime(PathString path) =>
        _pathPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Times loading and saving the session for a timed request. Runs straight after UseSession, so the session is
/// loaded here rather than on first use, and anything still unsaved is saved again by the session middleware.
/// </summary>
public sealed class RequestTimingSessionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (RequestTimings.Current is null)
        {
            await next(context);
            return;
        }

        using (RequestTimings.Measure("session load"))
            await context.Session.LoadAsync(context.RequestAborted);

        await next(context);

        using (RequestTimings.Measure("session save"))
            await context.Session.CommitAsync();
    }
}

public static class RequestTimingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestTiming(this IApplicationBuilder app)
        => app.UseMiddleware<RequestTimingMiddleware>();

    public static IApplicationBuilder UseRequestTimingSession(this IApplicationBuilder app)
        => app.UseMiddleware<RequestTimingSessionMiddleware>();
}
