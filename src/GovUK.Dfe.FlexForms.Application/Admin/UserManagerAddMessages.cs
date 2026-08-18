namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// User-facing copy for User Manager Add. Keep these strings identical to the previous PageModel.
/// </summary>
public static class UserManagerAddMessages
{
    public const string InvalidRole = "Select a valid role for this tenant.";

    public const string UserRoleRequiresTemplate = "Select at least one form for the User role.";

    public const string DuplicateEmail = "A user with this email address already exists in this tenant.";

    public const string AddFailed = "Could not add the user.";

    public const string LoadTemplatesFailed = "Could not load available forms.";

    public const string LoadRolesFailed = "Could not load available roles.";

    public static string Added(string email, string role) =>
        $"User {email} has been added with role {role}.";
}
