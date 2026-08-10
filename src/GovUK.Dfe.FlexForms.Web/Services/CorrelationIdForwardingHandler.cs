namespace GovUK.Dfe.FlexForms.Web.Services;

/// <summary>
/// Forwards (or generates) <c>x-correlationId</c> on outbound API HttpClient calls.
/// </summary>
public sealed class CorrelationIdForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    public const string HeaderName = Middleware.CorrelationIdMiddleware.HeaderName;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        string correlationId;

        if (httpContext?.Request.Headers.TryGetValue(HeaderName, out var existing) == true
            && Guid.TryParse(existing.ToString(), out var parsed)
            && parsed != Guid.Empty)
        {
            correlationId = parsed.ToString();
        }
        else
        {
            correlationId = Guid.NewGuid().ToString();
            if (httpContext is not null)
            {
                httpContext.Request.Headers[HeaderName] = correlationId;
            }
        }

        if (request.Headers.Contains(HeaderName))
            request.Headers.Remove(HeaderName);

        request.Headers.TryAddWithoutValidation(HeaderName, correlationId);

        return base.SendAsync(request, cancellationToken);
    }
}
