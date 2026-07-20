using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace GovUK.Dfe.FlexForms.Domain.Models;

/// <summary>
/// Represents an element that can be affected by conditional logic
/// </summary>
[ExcludeFromCodeCoverage]
public class AffectedElement
{
    /// <summary>
    /// The ID of the element to affect (fieldId, pageId, etc.)
    /// </summary>
    [JsonPropertyName("elementId")]
    public required string ElementId { get; set; }

    /// <summary>
    /// The type of element (field, page, fieldGroup)
    /// </summary>
    [JsonPropertyName("elementType")]
    public required string ElementType { get; set; }

    /// <summary>
    /// The action to perform on this element
    /// </summary>
    [JsonPropertyName("action")]
    public required string Action { get; set; }

    /// <summary>
    /// Additional configuration for the action
    /// </summary>
    [JsonPropertyName("actionConfig")]
    public Dictionary<string, object>? ActionConfig { get; set; }
}
