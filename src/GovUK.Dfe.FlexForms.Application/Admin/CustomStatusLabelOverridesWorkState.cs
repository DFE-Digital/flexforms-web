using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Mutable view-state bag for Custom Status Label Overrides.
/// </summary>
public sealed class CustomStatusLabelOverridesWorkState
{
    public Guid? SelectedTemplateId { get; set; }

    public ApplicationStatus SelectedBaseStatus { get; set; }

    public string BaseStatusOverrideValue { get; set; } = string.Empty;

    public FormTemplate? CurrentTemplate { get; set; }

    public string? CurrentVersionNumber { get; set; }

    public IReadOnlyList<TemplateDto> AvailableTemplates { get; set; } = [];

    public IReadOnlyList<KeyValuePair<ApplicationStatus, string>> BaseStatuses { get; set; } = [];
}
