namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// User-facing copy for User Manager. Keep these strings identical to the previous PageModel.
/// </summary>
public static class UserManagerMessages
{
    public const string Removed = "User removed from this tenant.";

    public const string RemoveFailed = "Could not remove the user from this tenant.";

    public const string LoadFailed = "Could not load users for this tenant.";

    public const string AuditLogLoadFailed =
        "Could not load the access audit trail. Ensure the API is up to date and database migrations have been applied.";
}
