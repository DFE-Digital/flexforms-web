using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Mutable view-state bag for the Role Manager Permissions admin page.
/// </summary>
public sealed class RoleManagerPermissionsWorkState
{
    public Guid RoleId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public bool IsSystemRole { get; set; }

    public List<string> SelectedGrants { get; set; } = [];

    public ResourceType NewResourceType { get; set; } = ResourceType.Application;

    public string? NewResourceKey { get; set; }

    public AccessType NewAccessType { get; set; } = AccessType.Read;
}
