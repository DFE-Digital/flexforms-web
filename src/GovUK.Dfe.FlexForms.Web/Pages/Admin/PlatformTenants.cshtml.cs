using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// SuperAdmin read-only list of all platform tenants.
/// </summary>
[Authorize(Policy = AdminAccessHelper.CanManageTenantSettingsPolicy)]
public sealed class PlatformTenantsModel(
    ITenantAdminClient tenantAdminClient,
    ILogger<PlatformTenantsModel> logger) : PageModel
{
    public string? Source { get; private set; }

    public int TenantCount { get; private set; }

    public DateTimeOffset? LastCatalogueRefreshUtc { get; private set; }

    public IReadOnlyList<PlatformTenantSummaryDto> Tenants { get; private set; } = [];

    public bool HasError { get; private set; }

    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await tenantAdminClient.GetPlatformTenantsAsync(cancellationToken);
            Source = response.Source;
            TenantCount = response.TenantCount;
            LastCatalogueRefreshUtc = response.LastCatalogueRefreshUtc;
            Tenants = response.Tenants?.ToList() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load platform tenants");
            HasError = true;
            ErrorMessage = TenantSettingsModel.GetErrorMessage(ex, "Could not load platform tenants.");
        }
    }
}
