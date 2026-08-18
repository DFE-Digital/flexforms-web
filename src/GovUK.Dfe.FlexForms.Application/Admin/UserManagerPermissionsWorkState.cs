using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Mutable view-state bag for the User Manager Permissions admin page.
/// </summary>
public sealed class UserManagerPermissionsWorkState
{
    public Guid UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string UserEmail { get; set; } = string.Empty;

    public List<string> SelectedGrants { get; set; } = [];

    public ResourceType NewResourceType { get; set; } = ResourceType.Application;

    public string? NewResourceKey { get; set; }

    public AccessType NewAccessType { get; set; } = AccessType.Read;
}
