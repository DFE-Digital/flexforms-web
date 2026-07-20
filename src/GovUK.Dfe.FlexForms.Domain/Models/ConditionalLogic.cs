using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace GovUK.Dfe.FlexForms.Domain.Models;

/// <summary>
/// Represents a conditional logic rule that controls form behavior based on user input
/// </summary>
[ExcludeFromCodeCoverage]
public class ConditionalLogic
{
    /// <summary>
    /// Unique identifier for this conditional logic rule
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name for this rule (for debugging/documentation)
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Priority for rule execution (lower numbers execute first)
    /// </summary>
    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 100;

    /// <summary>
    /// Whether this rule is currently enabled
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The condition group that determines when this rule should execute
    /// </summary>
    [JsonPropertyName("conditionGroup")]
    public required ConditionGroup ConditionGroup { get; set; }

    /// <summary>
    /// The elements that will be affected when this rule executes
    /// </summary>
    [JsonPropertyName("affectedElements")]
    public required List<AffectedElement> AffectedElements { get; set; }

    /// <summary>
    /// When this rule should be evaluated
    /// </summary>
    [JsonPropertyName("executeOn")]
    public List<string> ExecuteOn { get; set; } = new() { "change", "load" };

    /// <summary>
    /// Debounce time in milliseconds for real-time evaluation
    /// </summary>
    [JsonPropertyName("debounce")]
    public int Debounce { get; set; } = 300;
}