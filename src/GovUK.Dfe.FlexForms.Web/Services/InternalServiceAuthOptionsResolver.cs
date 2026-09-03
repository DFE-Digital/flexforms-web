using GovUK.Dfe.FlexForms.Web.Tenancy;
using GovUK.Dfe.CoreLibs.Security.Configurations;
using Microsoft.Extensions.DependencyInjection;

namespace GovUK.Dfe.FlexForms.Web.Services;

/// <summary>
/// Resolves InternalServiceAuth from the per-request tenant configuration with host fallback.
/// </summary>
public sealed class InternalServiceAuthOptionsResolver(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration hostConfiguration,
    ILogger<InternalServiceAuthOptionsResolver> logger) : IInternalServiceAuthOptionsResolver
{
    /// <inheritdoc />
    public InternalServiceAuthOptions Resolve()
    {
        var tenantContext = httpContextAccessor.HttpContext?.RequestServices
            .GetService<ITenantRequestContext>()
            ?? AmbientTenantRequestContext.Value;
        var tenantSection = tenantContext?.TenantConfiguration?
            .GetSection(InternalServiceAuthOptions.SectionName);

        // Prefer a complete tenant section so Services/ApiKey/SecretKey are isolated per tenant.
        // Do not Bind host first then tenant — IConfiguration.Bind appends list items.
        if (tenantSection?.Exists() == true && tenantSection.GetChildren().Any())
        {
            var tenantOptions = new InternalServiceAuthOptions();
            tenantSection.Bind(tenantOptions);
            logger.LogDebug(
                "Resolved InternalServiceAuth from tenant configuration ({TenantName})",
                tenantContext?.TenantName ?? tenantContext?.TenantId?.ToString() ?? "unknown");
            return tenantOptions;
        }

        var hostOptions = new InternalServiceAuthOptions();
        hostConfiguration.GetSection(InternalServiceAuthOptions.SectionName).Bind(hostOptions);

        // Empty scoped ITenantRequestContext exists on every request, including health/static
        // bypass paths where tenant middleware never runs. That is not a missing TenantConfig row.
        var tenantResolved = tenantContext?.TenantId is not null
            && tenantContext.TenantConfiguration is not null;
        if (!tenantResolved)
        {
            logger.LogDebug(
                "No tenant on this request; using host InternalServiceAuth.");
            return hostOptions;
        }

        logger.LogWarning(
            "Tenant '{TenantName}' has no InternalServiceAuth section; falling back to host secrets. " +
            "Configure a per-tenant InternalServiceAuth setting to isolate service credentials.",
            tenantContext!.TenantName ?? tenantContext.TenantId!.Value.ToString());
        return hostOptions;
    }
}
