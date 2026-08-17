namespace GovUK.Dfe.FlexForms.Application.FormEngine;

/// <summary>
/// User-facing copy for form-engine use cases. Keep these strings identical to the previous PageModel.
/// </summary>
public static class FormEngineMessages
{
    public const string NoWritePermission =
        "You do not have permission to make changes to this application.";

    public const string AllSectionsMustBeCompleted =
        "All sections must be completed before you can submit your application.";

    public const string ApplicationNotFound =
        "Application not found. Please try again.";

    public const string SelectAFile = "Select a file to upload";

    public const string DuplicateFileName =
        "The selected file has already been uploaded. Upload a file with a different name.\n ";

    public const string InvalidFileId = "Invalid file ID.";

    public const string FileDeleted = "File deleted.";

    public const string FieldIdAndItemIdRequired = "Field ID and Item ID are required";
}
