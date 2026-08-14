using GovUK.Dfe.FlexForms.Api.Client.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GovUK.Dfe.FlexForms.Web.Tenancy;

/// <summary>
/// Resolves API client settings from the current tenant configuration loaded by platform bootstrap.
/// Uses <see cref="IHttpContextAccessor"/> so settings remain correct when consumed from
/// HttpClient message handlers (which outlive a single request DI scope).
/// </summary>
public sealed class TenantApiClientSettingsProvider(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration hostConfiguration) : IApiClientSettingsProvider
{
    /// <inheritdoc />
    public ApiClientSettings GetSettings()
    {
        var settings = new ApiClientSettings();
        hostConfiguration.GetSection("ExternalApplicationsApiClient").Bind(settings);

        var tenantRequestContext = httpContextAccessor.HttpContext?.RequestServices
            .GetService<ITenantRequestContext>()
            ?? AmbientTenantRequestContext.Value;

        if (tenantRequestContext?.TenantConfiguration is { } tenantConfiguration)
        {
            tenantConfiguration.GetSection("ExternalApplicationsApiClient").Bind(settings);
        }

        if (tenantRequestContext?.TenantId.HasValue == true)
        {
            settings.TenantId = tenantRequestContext.TenantId;
        }

        return settings;
    }
}
