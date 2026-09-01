using System.Net;
using System.Text.Json;
using GovUK.Dfe.FlexForms.Web.Configuration;
using GovUK.Dfe.FlexForms.Web.Services.Tenant;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.Middleware;

/// <summary>
/// Resolves the current tenant (header or hostname) and loads its configuration from the platform API.
/// </summary>
public sealed class TenantConfigurationMiddleware(
    RequestDelegate next,
    IOptions<PlatformBootstrapOptions> bootstrapOptions,
    ILogger<TenantConfigurationMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        ITenantRequestContext tenantRequestContext,
        ITenantIdResolver tenantIdResolver,
        TenantConfigurationLoader tenantConfigurationLoader)
    {
        if (!bootstrapOptions.Value.Enabled)
        {
            await next(context);
            return;
        }

        if (ShouldBypassTenantResolution(context.Request.Path))
        {
            await next(context);
            return;
        }

        try
        {
            var tenantId = await tenantIdResolver.ResolveTenantIdAsync(context, context.RequestAborted);
            if (tenantId is null)
            {
                if (TenantIdResolver.IsNonPublicHostRequest(context))
                {
                    logger.LogDebug(
                        "Skipping tenant resolution for non-public host {Method} {Path} (Host={Host})",
                        context.Request.Method,
                        context.Request.Path,
                        context.Request.Host.Value);
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    return;
                }

                logger.LogWarning(
                    "No tenant could be resolved for {Method} {Path} (Host={Host})",
                    context.Request.Method,
                    context.Request.Path,
                    context.Request.Host.Value);

                await WriteErrorAsync(context, "Could not resolve tenant from request.");
                return;
            }

            var tenantConfig = await tenantConfigurationLoader.LoadAsync(tenantId.Value, context.RequestAborted);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(tenantConfig.Configuration.ToList())
                .Build();

            tenantRequestContext.TenantId = tenantConfig.TenantId;
            tenantRequestContext.TenantName = tenantConfig.TenantName;
            tenantRequestContext.TenantConfiguration = configuration;

            using (logger.BeginScope(new Dictionary<string, object>
                   {
                       ["TenantId"] = tenantConfig.TenantId,
                       ["TenantName"] = tenantConfig.TenantName
                   }))
            {
                await next(context);
            }
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug(
                "Tenant configuration load canceled because the client aborted {Method} {Path}",
                context.Request.Method,
                context.Request.Path);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to load tenant configuration from platform API");
            await WriteErrorAsync(context, "Failed to load tenant configuration from platform API.");
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = message }));
    }

    private static bool ShouldBypassTenantResolution(PathString path)
    {
        if (path.HasValue != true)
        {
            return false;
        }

        var value = path.Value!;
        return value.StartsWith("/css", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("/js", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("/lib", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("/assets", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("/govuk", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("/favicon", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase)
               || value.Equals("/health", StringComparison.OrdinalIgnoreCase)
               || value.Equals("/healthz", StringComparison.OrdinalIgnoreCase)
               || value.Equals("/liveness", StringComparison.OrdinalIgnoreCase)
               || value.Equals("/readiness", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("/Error", StringComparison.OrdinalIgnoreCase)
               || value.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase);
    }
}
