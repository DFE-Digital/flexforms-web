using GovUK.Dfe.FlexForms.Web.Configuration;

namespace GovUK.Dfe.FlexForms.Web.Services.Tenant;

/// <summary>
/// Caches tenant configuration loaded from the platform API.
/// </summary>
public interface ITenantConfigurationCache
{
    Task<PlatformTenantConfigurationResponse> GetOrAddAsync(
        Guid tenantId,
        Func<CancellationToken, Task<PlatformTenantConfigurationResponse>> factory,
        CancellationToken cancellationToken = default);
}
