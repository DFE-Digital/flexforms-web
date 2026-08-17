using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Mutable view-state bag for Template Manager.
/// </summary>
public sealed class TemplateManagerWorkState
{
    public Guid? SelectedTemplateId { get; set; }

    public string? SelectedVersionNumber { get; set; }

    public string? NewVersion { get; set; }

    public string? NewSchema { get; set; }

    public bool AcknowledgeReportingImpact { get; set; }

    public bool ShowAddVersionForm { get; set; }

    public FormTemplate? CurrentTemplate { get; set; }

    public string? CurrentVersionNumber { get; set; }

    public string? LatestVersionNumber { get; set; }

    public string? CurrentTemplateJson { get; set; }

    public bool HasError { get; set; }

    public string ErrorMessage { get; set; } = string.Empty;

    public IReadOnlyList<TemplateDto> TenantTemplates { get; set; } = [];

    public IReadOnlyList<TemplateVersionSummaryDto> AvailableVersions { get; set; } = [];

    public TemplateDto? SelectedTemplate { get; set; }

    public string? SessionVersionNumber { get; set; }

    public string? GrantToAllUsersSummary { get; set; }
}
