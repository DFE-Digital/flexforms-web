using GovUK.Dfe.FlexForms.Web.Configuration;
using GovUK.Dfe.FlexForms.Web.Services.Platform;

namespace GovUK.Dfe.FlexForms.Web.Services.Tenant;

/// <summary>
/// Loads tenant configuration from the platform API with in-memory caching.
/// </summary>
public sealed class TenantConfigurationLoader(
    PlatformConfigurationApiClient apiClient,
    ITenantConfigurationCache cache)
{
    public Task<PlatformTenantConfigurationResponse> LoadAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        cache.GetOrAddAsync(
            tenantId,
            ct => apiClient.GetTenantConfigurationAsync(tenantId, "Web", ct),
            cancellationToken);
}
