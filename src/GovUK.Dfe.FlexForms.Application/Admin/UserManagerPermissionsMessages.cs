namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// User-facing copy for User Manager Permissions. Keep these strings identical to the previous PageModel.
/// </summary>
public static class UserManagerPermissionsMessages
{
    public const string UserNotFound = "User not found.";

    public const string ResourceKeyRequired = "Enter a resource key.";

    public const string SaveFailed = "Could not save permissions.";

    public const string LoadPermissionsFailed = "Could not load user permissions.";

    public const string LoadUserFailed = "Could not load user.";

    public static string DuplicateGrant(string resourceType, string resourceKey, string accessType) =>
        $"{resourceType} / {resourceKey} / {accessType} is already in the list.";
}
