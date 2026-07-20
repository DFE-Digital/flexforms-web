using System.Diagnostics.CodeAnalysis;

namespace GovUK.Dfe.FlexForms.Domain.Models;

/// <summary>
/// Represents the conditional state of form elements
/// </summary>
[ExcludeFromCodeCoverage]
public class FormConditionalState
{
    /// <summary>
    /// Visibility state for fields (fieldId -> isVisible)
    /// </summary>
    public Dictionary<string, bool> FieldVisibility { get; set; } = new();

    /// <summary>
    /// Visibility state for pages (pageId -> isVisible)
    /// </summary>
    public Dictionary<string, bool> PageVisibility { get; set; } = new();

    /// <summary>
    /// Required state for fields (fieldId -> isRequired)
    /// </summary>
    public Dictionary<string, bool> FieldRequired { get; set; } = new();

    /// <summary>
    /// Enabled state for fields (fieldId -> isEnabled)
    /// </summary>
    public Dictionary<string, bool> FieldEnabled { get; set; } = new();

    /// <summary>
    /// Values to be set on fields (fieldId -> value)
    /// </summary>
    public Dictionary<string, object> FieldValues { get; set; } = new();

    /// <summary>
    /// Additional validation rules to apply (fieldId -> validationRules)
    /// </summary>
    public Dictionary<string, List<ValidationRule>> AdditionalValidations { get; set; } = new();

    /// <summary>
    /// Messages to display to the user
    /// </summary>
    public List<ConditionalLogicMessage> Messages { get; set; } = new();

    /// <summary>
    /// Pages that should be skipped
    /// </summary>
    public HashSet<string> SkippedPages { get; set; } = new();

    /// <summary>
    /// The result of the conditional logic evaluation
    /// </summary>
    public ConditionalLogicResult? EvaluationResult { get; set; }

    /// <summary>
    /// Timestamp when this state was calculated
    /// </summary>
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a message to be displayed based on conditional logic
/// </summary>
[ExcludeFromCodeCoverage]
public class ConditionalLogicMessage
{
    /// <summary>
    /// The message text
    /// </summary>
    public required string Text { get; set; }

    /// <summary>
    /// The message type (info, warning, error, success)
    /// </summary>
    public string Type { get; set; } = "info";

    /// <summary>
    /// The target element for this message
    /// </summary>
    public string? TargetElement { get; set; }

    /// <summary>
    /// The rule that generated this message
    /// </summary>
    public string? RuleId { get; set; }
}
