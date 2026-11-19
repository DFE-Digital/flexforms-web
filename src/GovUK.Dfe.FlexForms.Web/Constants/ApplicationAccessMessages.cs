namespace GovUK.Dfe.FlexForms.Web.Constants;

/// <summary>
/// User-facing messages for application access scenarios.
/// </summary>
public static class ApplicationAccessMessages
{
    /// <summary>
    /// Shown when a user attempts to edit an application they only have read access to.
    /// </summary>
    public const string NoWritePermission =
        "You do not have permission to make changes to this application.";

    public const string NoFileWritePermission =
        "You do not have permission to upload files for this application.";

    public const string NoFileReadPermission =
        "You do not have permission to download files for this application.";

    public const string NoFileDeletePermission =
        "You do not have permission to delete files for this application.";

    public const string NoNotificationDeletePermission =
        "You do not have permission to delete notifications.";

    public const string NoNotificationWritePermission =
        "You do not have permission to update notifications.";

    /// <summary>
    /// Shown when a user signs in to a tenant they have no membership in.
    /// Uses "authorised", not "unauthorized": the latter is treated as a token failure by the exception filter.
    /// </summary>
    public const string NotAuthorisedForTenant =
        "You are not authorised to access this service. If you think you should have access, contact your administrator.";

    /// <summary>
    /// Shown when a user cannot view a page or resource.
    /// </summary>
    public const string NoAccess =
        "You do not have permission to view this page or perform this action. If you think you should have access, contact your administrator.";
}
