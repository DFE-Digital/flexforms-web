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
}
