using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Application.Interfaces;

/// <summary>
/// Interface for evaluating conditional logic rules against form data
/// </summary>
public interface IConditionalLogicEngine
{
    /// <summary>
    /// Evaluates all conditional logic rules against the current form data
    /// </summary>
    /// <param name="conditionalLogic">The conditional logic rules to evaluate</param>
    /// <param name="formData">The current form data</param>
    /// <param name="context">Additional context for evaluation</param>
    /// <returns>The results of the evaluation</returns>
    ConditionalLogicResult EvaluateRules(IEnumerable<ConditionalLogic> conditionalLogic, 
        Dictionary<string, object> formData, 
        ConditionalLogicContext? context = null);

    /// <summary>
    /// Evaluates a single conditional logic rule
    /// </summary>
    /// <param name="rule">The rule to evaluate</param>
    /// <param name="formData">The current form data</param>
    /// <param name="context">Additional context for evaluation</param>
    /// <returns>Whether the rule's conditions are met</returns>
    bool EvaluateRule(ConditionalLogic rule, 
        Dictionary<string, object> formData, 
        ConditionalLogicContext? context = null);

    /// <summary>
    /// Evaluates a condition group
    /// </summary>
    /// <param name="conditionGroup">The condition group to evaluate</param>
    /// <param name="formData">The current form data</param>
    /// <param name="context">Additional context for evaluation</param>
    /// <returns>Whether the condition group is satisfied</returns>
    bool EvaluateConditionGroup(ConditionGroup conditionGroup, 
        Dictionary<string, object> formData, 
        ConditionalLogicContext? context = null);

    /// <summary>
    /// Evaluates a single condition
    /// </summary>
    /// <param name="condition">The condition to evaluate</param>
    /// <param name="formData">The current form data</param>
    /// <param name="context">Additional context for evaluation</param>
    /// <returns>Whether the condition is satisfied</returns>
    bool EvaluateCondition(Condition condition, 
        Dictionary<string, object> formData, 
        ConditionalLogicContext? context = null);

    /// <summary>
    /// Gets all rules that are triggered by a specific field
    /// </summary>
    /// <param name="conditionalLogic">The conditional logic rules</param>
    /// <param name="fieldId">The field ID that changed</param>
    /// <returns>Rules that should be re-evaluated</returns>
    IEnumerable<ConditionalLogic> GetTriggeredRules(IEnumerable<ConditionalLogic> conditionalLogic, string fieldId);

    /// <summary>
    /// Validates that a conditional logic rule is properly formed
    /// </summary>
    /// <param name="rule">The rule to validate</param>
    /// <returns>Validation result</returns>
    ConditionalLogicValidationResult ValidateRule(ConditionalLogic rule);
}
