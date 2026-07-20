using GovUK.Dfe.FlexForms.Web.Tenancy;
using GovUK.Dfe.CoreLibs.Security.Configurations;

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
            .GetService<ITenantRequestContext>();
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
                tenantContext?.TenantName ?? "unknown");
            return tenantOptions;
        }

        var hostOptions = new InternalServiceAuthOptions();
        hostConfiguration.GetSection(InternalServiceAuthOptions.SectionName).Bind(hostOptions);
        logger.LogDebug(
            "Resolved InternalServiceAuth from host configuration (tenant section missing)");
        return hostOptions;
    }
}
