using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Mutable view-state bag for the User Manager admin page.
/// </summary>
public sealed class UserManagerWorkState
{
    public IReadOnlyList<TenantUserDto> Users { get; set; } = [];

    public IReadOnlyList<TenantAccessAuditEntryDto> AccessAuditEntries { get; set; } = [];

    public bool HasError { get; set; }

    public string? ErrorMessage { get; set; }

    public bool AuditLogLoadFailed { get; set; }

    public string? AuditLogLoadErrorMessage { get; set; }
}
