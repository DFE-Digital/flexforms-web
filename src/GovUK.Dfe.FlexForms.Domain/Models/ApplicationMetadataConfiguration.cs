using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace GovUK.Dfe.FlexForms.Domain.Models;

/// <summary>
/// Optional metadata rows shown on the application task list (below the page heading),
/// configured per template in Template Manager JSON.
/// </summary>
[ExcludeFromCodeCoverage]
public class ApplicationMetadataConfiguration
{
    public const int MaxItems = 10;

    /// <summary>
    /// Ordered label/value rows. At most <see cref="MaxItems"/> entries are rendered.
    /// </summary>
    [JsonPropertyName("items")]
    public List<ApplicationMetadataItemDefinition>? Items { get; set; }
}

/// <summary>
/// A single metadata row. <see cref="FieldId"/> is a binding path (for example
/// <c>application.reference</c> or <c>field.incomingTrustName</c>).
/// </summary>
[ExcludeFromCodeCoverage]
public class ApplicationMetadataItemDefinition
{
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("fieldId")]
    public string? FieldId { get; set; }

    /// <summary>
    /// Sort order. Lower first. When omitted, array order is used (<c>10</c>, <c>20</c>, …).
    /// </summary>
    [JsonPropertyName("order")]
    public int? Order { get; set; }
}
