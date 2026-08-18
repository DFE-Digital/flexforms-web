using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Mutable view-state bag for the Admin home page.
/// </summary>
public sealed class AdminHomeWorkState
{
    public Guid? TenantId { get; set; }

    public bool IncludeTenantConfigurationSummary { get; set; }

    public bool SkipTemplateDetails { get; set; }

    public string? TemplateId { get; set; }

    public string? TemplateName { get; set; }

    public string? TemplateDescription { get; set; }

    public int TaskGroupCount { get; set; }

    public string? CurrentTemplateVersion { get; set; }

    public IReadOnlyList<TemplateDto> TenantTemplates { get; set; } = [];

    public TenantEffectiveConfigurationDto? TenantConfigurationSummary { get; set; }

    public TemplateDto? TemplateToOpen { get; set; }

    public bool HasError { get; set; }

    public string? ErrorMessage { get; set; }
}
