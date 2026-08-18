namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// User-facing copy for User Manager Edit. Keep these strings identical to the previous PageModel.
/// </summary>
public static class UserManagerEditMessages
{
    public const string UserNotFound = "User not found in this tenant.";

    public const string InvalidRole = "Select a valid role for this tenant.";

    public const string Updated = "User role and form access updated.";

    public const string UpdateFailed = "Could not update the user.";

    public const string LoadFailed = "Could not load user details.";

    public const string LoadTemplatesFailed = "Could not load available forms.";

    public const string LoadRolesFailed = "Could not load available roles.";
}
