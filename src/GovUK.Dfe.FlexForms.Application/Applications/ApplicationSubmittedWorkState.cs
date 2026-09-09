namespace GovUK.Dfe.FlexForms.Application.Applications;

/// <summary>
/// Display state for the post-submit confirmation page.
/// </summary>
public sealed class ApplicationSubmittedWorkState
{
    public string ReferenceNumber { get; set; } = string.Empty;

    public string PanelTitle { get; set; } = string.Empty;

    public string BodyMarkdown { get; set; } = string.Empty;
}
