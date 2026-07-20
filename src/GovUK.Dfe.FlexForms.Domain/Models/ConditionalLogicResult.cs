using System.Diagnostics.CodeAnalysis;

namespace GovUK.Dfe.FlexForms.Domain.Models;

/// <summary>
/// Result of conditional logic evaluation
/// </summary>
[ExcludeFromCodeCoverage]
public class ConditionalLogicResult
{
    /// <summary>
    /// Actions to be executed based on evaluated rules
    /// </summary>
    public List<ConditionalLogicAction> Actions { get; set; } = new();

    /// <summary>
    /// Any errors that occurred during evaluation
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Whether the evaluation was successful
    /// </summary>
    public bool IsSuccess => !Errors.Any();

    /// <summary>
    /// Rules that were evaluated
    /// </summary>
    public List<string> EvaluatedRules { get; set; } = new();
}

/// <summary>
/// An action to be executed as a result of conditional logic
/// </summary>
[ExcludeFromCodeCoverage]
public class ConditionalLogicAction
{
    /// <summary>
    /// The element to affect
    /// </summary>
    public required AffectedElement Element { get; set; }

    /// <summary>
    /// The rule that triggered this action
    /// </summary>
    public required string RuleId { get; set; }

    /// <summary>
    /// Priority of this action (lower numbers execute first)
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Timestamp when this action was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Context information for conditional logic evaluation
/// </summary>
[ExcludeFromCodeCoverage]
public class ConditionalLogicContext
{
    /// <summary>
    /// The current page ID
    /// </summary>
    public string? CurrentPageId { get; set; }

    /// <summary>
    /// The current task ID
    /// </summary>
    public string? CurrentTaskId { get; set; }

    /// <summary>
    /// Whether this is a client-side evaluation
    /// </summary>
    public bool IsClientSide { get; set; }

    /// <summary>
    /// The trigger that caused this evaluation
    /// </summary>
    public string? Trigger { get; set; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Result of conditional logic rule validation
/// </summary>
[ExcludeFromCodeCoverage]
public class ConditionalLogicValidationResult
{
    /// <summary>
    /// Whether the rule is valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Validation errors
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Validation warnings
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}
