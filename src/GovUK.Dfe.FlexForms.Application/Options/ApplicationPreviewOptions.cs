namespace GovUK.Dfe.FlexForms.Application.Options;

/// <summary>
/// Copy and visibility for the application check-your-answers (preview) page.
/// Bound from the <c>ApplicationPreview</c> section in tenant appsettings.
/// </summary>
public class ApplicationPreviewOptions
{
    /// <summary>
    /// Main page heading. When empty, falls back to "Check your answers".
    /// </summary>
    public string? PageHeading { get; set; }

    /// <summary>
    /// Heading above the submit section. When empty, falls back to terminology-based default.
    /// </summary>
    public string? SubmitHeading { get; set; }

    /// <summary>
    /// Confirmation hint under the submit heading. When empty, falls back to terminology-based default.
    /// </summary>
    public string? SubmitHint { get; set; }

    /// <summary>
    /// Label for the submit button. When empty, falls back to "Submit".
    /// </summary>
    public string? SubmitButtonText { get; set; }

    /// <summary>
    /// When true, hides the entire submit section (heading, hint, and button) on the preview page.
    /// </summary>
    public bool HideSubmitSection { get; set; }
}
