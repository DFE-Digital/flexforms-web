using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace GovUK.Dfe.FlexForms.Domain.Models.EventMapping;

/// <summary>
/// Defines the type of source for a field mapping
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FieldMappingSourceType
{
    /// <summary>
    /// Direct field value from form data
    /// </summary>
    DirectField,

    /// <summary>
    /// Extract a property from a complex field (e.g., autocomplete.ukprn)
    /// </summary>
    ComplexFieldProperty,

    /// <summary>
    /// Extract from metadata (e.g., applicationId, applicationReference)
    /// </summary>
    Metadata,

    /// <summary>
    /// Compute value from multiple fields
    /// </summary>
    Computed,

    /// <summary>
    /// Static value or generated value (e.g., current date/time)
    /// </summary>
    Static,

    /// <summary>
    /// Extract from a collection flow (multi-collection or derived)
    /// </summary>
    Collection
}

