using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Application.Interfaces;

/// <summary>
/// Interface for orchestrating conditional logic execution across the form engine
/// </summary>
public interface IConditionalLogicOrchestrator
{
    /// <summary>
    /// Applies conditional logic to form data and returns the updated form state
    /// </summary>
    /// <param name="template">The form template containing conditional logic rules</param>
    /// <param name="formData">The current form data</param>
    /// <param name="context">Additional context for evaluation</param>
    /// <returns>The form state after applying conditional logic</returns>
    Task<FormConditionalState> ApplyConditionalLogicAsync(FormTemplate template, 
        Dictionary<string, object> formData, 
        ConditionalLogicContext? context = null);

    /// <summary>
    /// Evaluates show/hide visibility for fields only — skips full template page/field initialization.
    /// Used for collection-item summary columns where a full ApplyConditionalLogicAsync per item is too expensive.
    /// </summary>
    /// <param name="fieldIds">
    /// Fields to resolve. When null, all fields with show/hide rules are evaluated.
    /// </param>
    Task<FormConditionalState> ApplyFieldVisibilityAsync(
        FormTemplate template,
        Dictionary<string, object> formData,
        IReadOnlyCollection<string>? fieldIds = null,
        ConditionalLogicContext? context = null);

    /// <summary>
    /// Evaluates conditional logic for a specific field change
    /// </summary>
    /// <param name="template">The form template containing conditional logic rules</param>
    /// <param name="formData">The current form data</param>
    /// <param name="changedFieldId">The field that was changed</param>
    /// <param name="context">Additional context for evaluation</param>
    /// <returns>The conditional logic result for the field change</returns>
    Task<ConditionalLogicResult> EvaluateFieldChangeAsync(FormTemplate template,
        Dictionary<string, object> formData,
        string changedFieldId,
        ConditionalLogicContext? context = null);

    /// <summary>
    /// Gets the visibility state for all fields and pages based on current data
    /// </summary>
    /// <param name="template">The form template</param>
    /// <param name="formData">The current form data</param>
    /// <param name="context">Additional context for evaluation</param>
    /// <returns>Visibility state for all form elements</returns>
    Task<Dictionary<string, bool>> GetElementVisibilityAsync(FormTemplate template,
        Dictionary<string, object> formData,
        ConditionalLogicContext? context = null);

    /// <summary>
    /// Gets the required state for all fields based on current data
    /// </summary>
    /// <param name="template">The form template</param>
    /// <param name="formData">The current form data</param>
    /// <param name="context">Additional context for evaluation</param>
    /// <returns>Required state for all form fields</returns>
    Task<Dictionary<string, bool>> GetFieldRequiredStateAsync(FormTemplate template,
        Dictionary<string, object> formData,
        ConditionalLogicContext? context = null);

    /// <summary>
    /// Validates all conditional logic rules in a template
    /// </summary>
    /// <param name="template">The form template to validate</param>
    /// <returns>Validation results for all rules</returns>
    Task<List<ConditionalLogicValidationResult>> ValidateTemplateRulesAsync(FormTemplate template);

    /// <summary>
    /// Gets the next page in the form flow based on conditional logic
    /// </summary>
    /// <param name="template">The form template</param>
    /// <param name="formData">The current form data</param>
    /// <param name="currentPageId">The current page ID</param>
    /// <param name="context">Additional context for evaluation</param>
    /// <returns>The next page ID, or null if at the end</returns>
    Task<string?> GetNextPageAsync(FormTemplate template,
        Dictionary<string, object> formData,
        string currentPageId,
        ConditionalLogicContext? context = null);

    /// <summary>
    /// Determines if a page should be skipped based on conditional logic
    /// </summary>
    /// <param name="template">The form template</param>
    /// <param name="formData">The current form data</param>
    /// <param name="pageId">The page ID to check</param>
    /// <param name="context">Additional context for evaluation</param>
    /// <returns>True if the page should be skipped</returns>
    Task<bool> ShouldSkipPageAsync(FormTemplate template,
        Dictionary<string, object> formData,
        string pageId,
        ConditionalLogicContext? context = null);
}
