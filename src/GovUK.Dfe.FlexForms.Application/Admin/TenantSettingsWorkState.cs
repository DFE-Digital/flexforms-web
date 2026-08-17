using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Mutable view-state bag for the Tenant Settings admin page.
/// </summary>
public sealed class TenantSettingsWorkState
{
    public Guid TenantId { get; set; }

    public string TenantName { get; set; } = string.Empty;

    public IReadOnlyList<TenantSettingDto> Settings { get; set; } = [];

    public TenantEffectiveConfigurationDto? EffectiveConfig { get; set; }

    public TenantHealthDto? TenantHealth { get; set; }

    public IReadOnlyList<TenantSettingCategoryCookbookEntryDto> Cookbook { get; set; } = [];

    public IReadOnlyList<TenantSettingAuditEntryDto> AuditEntries { get; set; } = [];

    public ValidateTenantSettingResponse? ValidationPreview { get; set; }

    public string? ValidationCategory { get; set; }

    public string? ValidationTarget { get; set; }

    public bool ValidationIsSecret { get; set; }

    public bool HasError { get; set; }

    public string? ErrorMessage { get; set; }
}
