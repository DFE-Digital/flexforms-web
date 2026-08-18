using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Mutable view-state bag for the Role Manager admin page.
/// </summary>
public sealed class RoleManagerWorkState
{
    public IReadOnlyList<TenantRoleDto> Roles { get; set; } = [];

    public bool HasError { get; set; }

    public string? ErrorMessage { get; set; }

    public string NewRoleName { get; set; } = string.Empty;
}
