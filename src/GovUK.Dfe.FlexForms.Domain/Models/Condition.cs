using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace GovUK.Dfe.FlexForms.Domain.Models;

/// <summary>
/// Represents a single condition in a conditional logic rule
/// </summary>
[ExcludeFromCodeCoverage]
public class Condition
{
    /// <summary>
    /// The field that triggers this condition
    /// </summary>
    [JsonPropertyName("triggerField")]
    public required string TriggerField { get; set; }

    /// <summary>
    /// The comparison operator to use
    /// </summary>
    [JsonPropertyName("operator")]
    public required string Operator { get; set; }

    /// <summary>
    /// The value to compare against
    /// </summary>
    [JsonPropertyName("value")]
    public required object Value { get; set; }

    /// <summary>
    /// The data type for type-specific operations
    /// </summary>
    [JsonPropertyName("dataType")]
    public string DataType { get; set; } = "string";

    /// <summary>
    /// Logical operator for nested condition groups
    /// </summary>
    [JsonPropertyName("logicalOperator")]
    public string? LogicalOperator { get; set; }

    /// <summary>
    /// Nested conditions for complex logic
    /// </summary>
    [JsonPropertyName("conditions")]
    public List<Condition>? Conditions { get; set; }
}