using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Domain.Models;

[ExcludeFromCodeCoverage]
public class Page
{
    [JsonPropertyName("pageId")]
    public required string PageId { get; set; }

    [JsonPropertyName("slug")]
    public required string Slug { get; set; }

    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("description")]
    public required string Description { get; set; }

    [JsonPropertyName("pageOrder")]
    public required int PageOrder { get; set; }

    [JsonPropertyName("fields")]
    public required List<Field> Fields { get; set; }

    [JsonPropertyName("returnToSummaryPage")]
    public bool ReturnToSummaryPage { get; set; } = true; // Default to true for backward compatibility

    /// <summary>
    /// Optional. When set, overrides <see cref="ReturnToSummaryPage"/> for post-save navigation.
    /// Omitted in existing templates so they keep using <see cref="ReturnToSummaryPage"/>.
    /// </summary>
    [JsonPropertyName("navigationAfterSave")]
    public NavigationAfterSave? NavigationAfterSave { get; set; }

    [JsonPropertyName("saveButtonLabel")]
    public string? SaveButtonLabel { get; set; }
}