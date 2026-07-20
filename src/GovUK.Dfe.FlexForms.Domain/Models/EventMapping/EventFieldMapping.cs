using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace GovUK.Dfe.FlexForms.Domain.Models.EventMapping;

/// <summary>
/// Defines how form template fields map to event properties
/// </summary>
[ExcludeFromCodeCoverage]
public class EventFieldMapping
{
    /// <summary>
    /// Unique identifier for this mapping configuration
    /// </summary>
    [JsonPropertyName("mappingId")]
    public required string MappingId { get; set; }

    /// <summary>
    /// The event type this mapping is for (e.g., "TransferApplicationSubmittedEvent")
    /// </summary>
    [JsonPropertyName("eventType")]
    public required string EventType { get; set; }

    /// <summary>
    /// Description of what this mapping does
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Dictionary of event property names to their field mappings
    /// </summary>
    [JsonPropertyName("fieldMappings")]
    public required Dictionary<string, FieldMapping> FieldMappings { get; set; }
}

