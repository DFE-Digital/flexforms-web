namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// User-facing copy for Role Manager Permissions. Keep these strings identical to the previous PageModel.
/// </summary>
public static class RoleManagerPermissionsMessages
{
    public const string SystemRoleCannotChange = "System role permissions cannot be changed.";

    public const string SystemRoleCannotChangeCreateCustom =
        "System role permissions cannot be changed. Create a custom role instead.";

    public const string RoleNotFound = "Role not found.";

    public const string ResourceKeyRequired = "Enter a resource key.";

    public const string SaveFailed = "Could not save permissions.";

    public const string LoadPermissionsFailed = "Could not load role permissions.";

    public const string LoadRoleFailed = "Could not load role.";

    public static string DuplicateGrant(string resourceType, string resourceKey, string accessType) =>
        $"{resourceType} / {resourceKey} / {accessType} is already in the list.";
}
