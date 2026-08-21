namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Mutable view-state bag for Organisation Settings.
/// </summary>
public sealed class OrganisationSettingsWorkState
{
    public Guid TenantId { get; set; }

    public string TenantName { get; set; } = string.Empty;

    public string TerminologySingular { get; set; } = "application";

    public string TerminologyPlural { get; set; } = "applications";

    public bool BannerEnabled { get; set; }

    public string? BannerHeading { get; set; } = "Important";

    public string? BannerMessage { get; set; } = string.Empty;

    public int DashboardPageSize { get; set; } = 50;

    public bool DashboardEnableFilters { get; set; }

    public string? DashboardMainHeading { get; set; }

    public string? DashboardInProgressHeading { get; set; }

    public string? DashboardStartNewHeading { get; set; }

    public string? DashboardStartNewHint { get; set; }

    public string? DashboardStartNewButtonText { get; set; }

    public bool HasError { get; set; }

    public string? ErrorMessage { get; set; }
}
