using System.Security.Claims;
using GovUK.Dfe.CoreLibs.Http.Interfaces;
using GovUK.Dfe.FlexForms.Web.Telemetry;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.Middleware;

/// <summary>
/// Populates CoreLibs request telemetry plus FlexForms form/application scope after authentication.
/// </summary>
public sealed class RequestTelemetryEnrichmentMiddleware(
    RequestDelegate next,
    IOptions<SessionOptions> sessionOptions,
    ReportingUserIdGenerator reportingUserIdGenerator,
    ILogger<RequestTelemetryEnrichmentMiddleware> logger)
{
    private readonly string _sessionCookieName = sessionOptions.Value.Cookie.Name!;

    public async Task InvokeAsync(
        HttpContext context,
        ITenantRequestContext tenantRequestContext,
        IRequestTelemetryContext telemetry,
        IFlexFormsRequestScope flexFormsScope,
        ICorrelationContext correlationContext)
    {
        if (tenantRequestContext.TenantId.HasValue)
        {
            telemetry.TenantId = tenantRequestContext.TenantId.Value.ToString();
            telemetry.TenantName = tenantRequestContext.TenantName;
        }

        if (context.User?.Identity?.IsAuthenticated == true)
        {
            // Pseudonymous reporting id, never the raw NameIdentifier: log scopes reach Application Insights.
            telemetry.UserId = tenantRequestContext.TenantId is Guid tenantId
                ? reportingUserIdGenerator.Create(tenantId, context.User.FindFirstValue(ClaimTypes.NameIdentifier))
                : null;
            var rawEmail = context.User.FindFirstValue(ClaimTypes.Email)
                ?? context.User.Identity?.Name;
            telemetry.UserEmail = string.IsNullOrWhiteSpace(rawEmail)
                ? null
                : PiiMasking.MaskEmail(rawEmail);
        }

        // Reading the session is a blocking Redis call, even for a brand-new session. Without a session cookie
        // there is nothing to read, so skip it; otherwise every cookieless request stalls while Redis is down.
        if (context.Request.Cookies.ContainsKey(_sessionCookieName))
        {
            var templateId = context.Session.GetString("TemplateId");
            if (!string.IsNullOrWhiteSpace(templateId))
                flexFormsScope.TemplateId = templateId;

            var applicationReference = context.Session.GetString("ApplicationReference");
            if (!string.IsNullOrWhiteSpace(applicationReference))
                flexFormsScope.ApplicationReference = applicationReference;

            ReportingTelemetryItems.BindSession(context, context.Session);
        }

        telemetry.CorrelationId ??= correlationContext.CorrelationId.ToString();
        telemetry.ServiceName = "flexforms-web";

        var scope = new Dictionary<string, object>(telemetry.ToScopeDictionary(), StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in flexFormsScope.ToScopeDictionary())
            scope[kvp.Key] = kvp.Value;

        using (logger.BeginScope(scope))
        {
            await next(context);
        }
    }
}

/// <summary>
/// Binds the reporting session id straight after authentication, so telemetry from the token, activity and
/// permissions middleware carries it. Same session-cookie guard as <see cref="RequestTelemetryEnrichmentMiddleware"/>.
/// </summary>
public sealed class ReportingSessionBindingMiddleware(RequestDelegate next, IOptions<SessionOptions> sessionOptions)
{
    private readonly string _sessionCookieName = sessionOptions.Value.Cookie.Name!;

    public Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Cookies.ContainsKey(_sessionCookieName))
            ReportingTelemetryItems.BindSessionEarly(context, context.Session);

        return next(context);
    }
}

public static class RequestTelemetryEnrichmentMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestTelemetryEnrichment(this IApplicationBuilder app)
        => app.UseMiddleware<RequestTelemetryEnrichmentMiddleware>();

    public static IApplicationBuilder UseReportingSessionBinding(this IApplicationBuilder app)
        => app.UseMiddleware<ReportingSessionBindingMiddleware>();
}
