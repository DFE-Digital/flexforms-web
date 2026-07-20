using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace GovUK.Dfe.FlexForms.Domain.Models.EventMapping;

/// <summary>
/// Defines how a single field or set of fields map to an event property
/// </summary>
[ExcludeFromCodeCoverage]
public class FieldMapping
{
    /// <summary>
    /// The source field ID in the form template (for DirectField and ComplexFieldProperty)
    /// </summary>
    [JsonPropertyName("sourceFieldId")]
    public string? SourceFieldId { get; set; }

    /// <summary>
    /// Multiple source field IDs (for Computed mappings)
    /// </summary>
    [JsonPropertyName("sourceFieldIds")]
    public List<string>? SourceFieldIds { get; set; }

    /// <summary>
    /// The type of mapping to perform
    /// </summary>
    [JsonPropertyName("sourceType")]
    public FieldMappingSourceType SourceType { get; set; } = FieldMappingSourceType.DirectField;

    /// <summary>
    /// Transformation to apply to the extracted value
    /// </summary>
    [JsonPropertyName("transformationType")]
    public string? TransformationType { get; set; }

    /// <summary>
    /// Additional configuration for transformations
    /// </summary>
    [JsonPropertyName("transformationConfig")]
    public Dictionary<string, object>? TransformationConfig { get; set; }

    /// <summary>
    /// Default value if the field is not found or empty
    /// </summary>
    [JsonPropertyName("defaultValue")]
    public object? DefaultValue { get; set; }

    /// <summary>
    /// Nested property path for complex fields (e.g., "ukprn" or "groupName")
    /// </summary>
    [JsonPropertyName("nestedPath")]
    public string? NestedPath { get; set; }

    /// <summary>
    /// Configuration for mapping collection/array data
    /// </summary>
    [JsonPropertyName("collectionMapping")]
    public CollectionMapping? CollectionMapping { get; set; }
}

