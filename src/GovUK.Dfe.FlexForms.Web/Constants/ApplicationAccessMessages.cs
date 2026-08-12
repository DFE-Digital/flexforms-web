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

    /// <summary>
    /// Shown when a user cannot view a page or resource.
    /// </summary>
    public const string NoAccess =
        "You do not have permission to view this page or perform this action. If you think you should have access, contact your administrator.";
}
