using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Mutable view-state bag for the Reporting export admin page.
/// </summary>
public sealed class ReportingExportWorkState
{
    public Guid TenantId { get; set; }

    public Guid? SelectedTemplateId { get; set; }

    public Guid? RefreshId { get; set; }

    public IReadOnlyList<TemplateDto> AvailableTemplates { get; set; } = [];

    public ReportingExportDefaultDto? TenantDefault { get; set; }

    public ReportingExportDefaultDto? TemplateDefault { get; set; }

    public ReportingExportPolicyDto? Policy { get; set; }

    /// <summary>Why the template's fields can't be shown, for example because reporting hasn't seen the template yet.</summary>
    public string? PolicyMessage { get; set; }

    public ReportingExportRefreshDto? Refresh { get; set; }

    public bool HasError { get; set; }

    public string? ErrorMessage { get; set; }
}
