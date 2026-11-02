namespace GovUK.Dfe.FlexForms.Application.Options;

/// <summary>
/// Confirmation copy stored under TenantConfig category <c>ApplicationSubmittedPage</c>.
/// <see cref="DefaultTemplateKey"/> is the tenant-wide fallback. Other keys (template GUIDs or
/// schema aliases) are still read for templates that have not moved copy onto
/// <c>displayCopy.submittedPage</c>.
/// </summary>
public sealed class ApplicationSubmittedPageCopy
{
    public const string DefaultTemplateKey = "_default";

    public string? PanelTitle { get; set; }

    public string? BodyMarkdown { get; set; }
}
