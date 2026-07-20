using GovUK.Dfe.FlexForms.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace GovUK.Dfe.FlexForms.Web.Tenancy;

/// <summary>
/// Exposes merged host + tenant configuration for the current HTTP request.
/// </summary>
public interface ITenantAppConfiguration : IRequestAppConfiguration
{
}

/// <inheritdoc />
public sealed class TenantAppConfiguration(
    ITenantRequestContext tenantRequestContext,
    IConfiguration hostConfiguration) : ITenantAppConfiguration
{
    private IConfiguration? _effective;

    /// <inheritdoc />
    public IConfiguration Current => _effective ??= BuildEffectiveConfiguration();

    /// <inheritdoc />
    public string? this[string key] => Current[key];

    /// <inheritdoc />
    public IConfigurationSection GetSection(string key) => Current.GetSection(key);

    private IConfiguration BuildEffectiveConfiguration()
    {
        if (tenantRequestContext.TenantConfiguration is null)
        {
            return hostConfiguration;
        }

        return new ConfigurationBuilder()
            .AddConfiguration(hostConfiguration)
            .AddConfiguration(tenantRequestContext.TenantConfiguration)
            .Build();
    }
}
