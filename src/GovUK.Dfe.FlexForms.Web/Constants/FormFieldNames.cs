namespace GovUK.Dfe.FlexForms.Web.Constants;

/// <summary>
/// Names of HTML form fields posted by FormEngine views (hidden inputs, file inputs, checkboxes).
/// Keep these in sync with any view that renders a matching input, and with the handlers that read
/// Request.Form / Request.Form.Files by name: the name attribute and the server-side lookup must match
/// exactly, and the compiler cannot catch a mismatch between a Razor view and C# string literals.
/// </summary>
public static class FormFieldNames
{
    public const string ApplicationId = "ApplicationId";
    public const string FieldId = "FieldId";
    public const string ReturnUrl = "ReturnUrl";
    public const string FileId = "FileId";
    public const string UploadDescription = "UploadDescription";
    public const string ChooseFile = "ChooseFile";
    public const string IsTaskCompleted = "IsTaskCompleted";
    public const string Confirmed = "Confirmed";
}
