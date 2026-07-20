using Microsoft.Extensions.Configuration;

namespace GovUK.Dfe.FlexForms.Web.Tenancy;

/// <summary>
/// Helpers for reading tenant-scoped configuration values.
/// </summary>
public static class TenantConfigurationExtensions
{
    /// <summary>
    /// Returns a configuration value from tenant configuration when available, otherwise from host configuration.
    /// </summary>
    public static string? GetTenantOrHostValue(
        this ITenantRequestContext tenantContext,
        IConfiguration hostConfiguration,
        string key)
    {
        return tenantContext.TenantConfiguration?[key] ?? hostConfiguration[key];
    }

    /// <summary>
    /// Returns a configuration section from tenant configuration when available, otherwise from host configuration.
    /// </summary>
    public static IConfigurationSection GetTenantOrHostSection(
        this ITenantRequestContext tenantContext,
        IConfiguration hostConfiguration,
        string key)
    {
        var tenantSection = tenantContext.TenantConfiguration?.GetSection(key);
        if (tenantSection?.Exists() == true)
        {
            return tenantSection;
        }

        return hostConfiguration.GetSection(key);
    }
}
