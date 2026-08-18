using System.Security.Claims;
using GovUK.Dfe.CoreLibs.Http.Interfaces;
using GovUK.Dfe.FlexForms.Web.Telemetry;
using GovUK.Dfe.FlexForms.Web.Tenancy;

namespace GovUK.Dfe.FlexForms.Web.Middleware;

/// <summary>
/// Populates CoreLibs request telemetry plus FlexForms form/application scope after authentication.
/// </summary>
public sealed class RequestTelemetryEnrichmentMiddleware(
    RequestDelegate next,
    ILogger<RequestTelemetryEnrichmentMiddleware> logger)
{
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
            telemetry.UserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            telemetry.UserEmail = context.User.FindFirstValue(ClaimTypes.Email)
                ?? context.User.Identity?.Name;
        }

        var templateId = context.Session.GetString("TemplateId");
        if (!string.IsNullOrWhiteSpace(templateId))
            flexFormsScope.TemplateId = templateId;

        var applicationReference = context.Session.GetString("ApplicationReference");
        if (!string.IsNullOrWhiteSpace(applicationReference))
            flexFormsScope.ApplicationReference = applicationReference;

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

public static class RequestTelemetryEnrichmentMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestTelemetryEnrichment(this IApplicationBuilder app)
        => app.UseMiddleware<RequestTelemetryEnrichmentMiddleware>();
}
