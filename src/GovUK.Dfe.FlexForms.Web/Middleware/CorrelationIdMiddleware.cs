namespace GovUK.Dfe.FlexForms.Web.Middleware;

/// <summary>
/// Ensures every browser request has an <c>x-correlationId</c> so API calls can share the same id.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "x-correlationId";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(HeaderName, out var existing)
            || !Guid.TryParse(existing.ToString(), out var correlationId)
            || correlationId == Guid.Empty)
        {
            correlationId = Guid.NewGuid();
            context.Request.Headers[HeaderName] = correlationId.ToString();
        }

        context.Response.Headers[HeaderName] = correlationId.ToString();

        using (logger.BeginScope("x-correlationId: {CorrelationId}", correlationId.ToString()))
        {
            await next(context);
        }
    }
}
