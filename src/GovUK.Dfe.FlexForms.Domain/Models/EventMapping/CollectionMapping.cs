using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace GovUK.Dfe.FlexForms.Domain.Models.EventMapping;

/// <summary>
/// Configuration for mapping collection/array data from multi-collection flows
/// </summary>
[ExcludeFromCodeCoverage]
public class CollectionMapping
{
    /// <summary>
    /// The field ID of the collection in the form template (e.g., "detailsOfAcademies")
    /// </summary>
    [JsonPropertyName("sourceCollectionFieldId")]
    public required string SourceCollectionFieldId { get; set; }

    /// <summary>
    /// If true, extract only the first item from the collection (for single-item collections)
    /// </summary>
    [JsonPropertyName("extractFirst")]
    public bool ExtractFirst { get; set; }

    /// <summary>
    /// Nested path to extract from collection items (e.g., "trustsSearch-field-flow.ukprn")
    /// Used when ExtractFirst is true
    /// </summary>
    [JsonPropertyName("nestedPath")]
    public string? NestedPath { get; set; }

    /// <summary>
    /// Mappings for each property of items in the collection
    /// Used to transform collection items to event model structure
    /// </summary>
    [JsonPropertyName("itemMappings")]
    public Dictionary<string, FieldMapping>? ItemMappings { get; set; }

    /// <summary>
    /// Optional filter condition to apply to collection items
    /// </summary>
    [JsonPropertyName("filterCondition")]
    public string? FilterCondition { get; set; }
}

