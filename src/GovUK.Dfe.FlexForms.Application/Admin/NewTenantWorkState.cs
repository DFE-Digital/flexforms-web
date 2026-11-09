namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Mutable view-state bag for the New tenant admin page.
/// </summary>
public sealed class NewTenantWorkState
{
    public Guid SourceTenantId { get; set; }

    public string SourceTenantName { get; set; } = string.Empty;

    public Guid NewTenantId { get; set; }

    public string NewTenantName { get; set; } = string.Empty;

    public string Hostname { get; set; } = string.Empty;

    public string FrontendOrigin { get; set; } = string.Empty;

    /// <summary>
    /// Interactive sign-in scheme for the new tenant: Test Authentication when
    /// <see cref="TestAuthenticationAllowed"/>, otherwise the current tenant's scheme.
    /// </summary>
    public string Scheme { get; set; } = string.Empty;

    /// <summary>Whether this environment allows Test Authentication (never Production).</summary>
    public bool TestAuthenticationAllowed { get; set; }

    public List<NewTenantSettingDraft> Settings { get; set; } = [];

    public bool HasError { get; set; }

    public string? ErrorMessage { get; set; }
}

/// <summary>
/// One TenantConfig row the new tenant will be created with. A blank <see cref="SettingsJson"/> is not sent.
/// </summary>
public sealed class NewTenantSettingDraft
{
    public string Category { get; set; } = string.Empty;

    public string Target { get; set; } = string.Empty;

    public string SettingsJson { get; set; } = string.Empty;

    /// <summary>Copied for convenience; the tenant runs without it.</summary>
    public bool IsOptional { get; set; }
}
