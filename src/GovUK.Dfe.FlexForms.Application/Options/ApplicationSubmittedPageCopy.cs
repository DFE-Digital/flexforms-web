namespace GovUK.Dfe.FlexForms.Application.Options;

/// <summary>
/// Per-template confirmation copy stored under TenantConfig category <c>ApplicationSubmittedPage</c>.
/// </summary>
public sealed class ApplicationSubmittedPageCopy
{
    public const string DefaultTemplateKey = "_default";

    public string? PanelTitle { get; set; }

    public string? BodyMarkdown { get; set; }
}
