using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Mutable view-state bag for the User Manager admin page.
/// </summary>
public sealed class UserManagerWorkState
{
    public const int PageSize = 10;

    public IReadOnlyList<TenantUserDto> Users { get; set; } = [];

    public IReadOnlyList<TenantAccessAuditEntryDto> AccessAuditEntries { get; set; } = [];

    public bool HasError { get; set; }

    public string? ErrorMessage { get; set; }

    public bool AuditLogLoadFailed { get; set; }

    public string? AuditLogLoadErrorMessage { get; set; }

    public int CurrentPage { get; set; } = 1;

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }
}
