using Microsoft.AspNetCore.Http.Features;

namespace GovUK.Dfe.FlexForms.Web.Services;

/// <summary>
/// Forwards (or generates) <c>x-correlationId</c> on outbound API HttpClient calls.
/// </summary>
public sealed class CorrelationIdForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    public const string HeaderName = "x-correlationId";

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

        if (httpContext is not null)
        {
            ForwardTelemetryHeader(request, httpContext, "X-Template-Id", "TemplateId");
            ForwardTelemetryHeader(request, httpContext, "X-Application-Reference", "ApplicationReference");
        }

        return base.SendAsync(request, cancellationToken);
    }

    private static void ForwardTelemetryHeader(
        HttpRequestMessage request,
        HttpContext httpContext,
        string headerName,
        string sessionKey)
    {
        string? value = null;

        if (httpContext.Request.Headers.TryGetValue(headerName, out var fromRequest)
            && !string.IsNullOrWhiteSpace(fromRequest))
        {
            value = fromRequest.ToString();
        }
        else
        {
            // Tenant bootstrap HTTP calls run before UseSession(); skip session then.
            var session = httpContext.Features.Get<ISessionFeature>()?.Session;
            if (session is not null)
                value = session.GetString(sessionKey);
        }

        if (string.IsNullOrWhiteSpace(value))
            return;

        if (request.Headers.Contains(headerName))
            request.Headers.Remove(headerName);

        request.Headers.TryAddWithoutValidation(headerName, value);
    }
}
