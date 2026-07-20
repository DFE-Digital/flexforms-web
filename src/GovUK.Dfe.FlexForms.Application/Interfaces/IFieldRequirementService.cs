using GovUK.Dfe.FlexForms.Domain.Models;
using DomainTask = GovUK.Dfe.FlexForms.Domain.Models.Task;

namespace GovUK.Dfe.FlexForms.Application.Interfaces;

/// <summary>
/// Service to determine if a field is required based on template policy and field configuration
/// </summary>
public interface IFieldRequirementService
{
    /// <summary>
    /// Determines if a field is required based on the template's default policy,
    /// field's Required property, and field's validation rules
    /// </summary>
    /// <param name="field">The field to check</param>
    /// <param name="template">The form template containing the default policy</param>
    /// <returns>True if the field is required</returns>
    bool IsFieldRequired(Field field, FormTemplate template);

    /// <summary>
    /// Gets all required fields for a task
    /// </summary>
    /// <param name="task">The task to check</param>
    /// <param name="template">The form template containing the default policy</param>
    /// <param name="isFieldHidden">Optional predicate to check if a field is hidden by conditional logic</param>
    /// <returns>List of required field IDs</returns>
    List<string> GetRequiredFieldsForTask(DomainTask task, FormTemplate template, Func<string, bool>? isFieldHidden = null);

    /// <summary>
    /// Validates that all required fields in a task have values
    /// </summary>
    /// <param name="task">The task to validate</param>
    /// <param name="template">The form template</param>
    /// <param name="formData">The form data</param>
    /// <param name="isFieldHidden">Optional predicate to check if a field is hidden by conditional logic</param>
    /// <returns>List of field IDs that are required but missing values</returns>
    List<string> GetMissingRequiredFields(DomainTask task, FormTemplate template, Dictionary<string, object> formData, Func<string, bool>? isFieldHidden = null);

    /// <summary>
    /// Validates that all required fields in a task have values and returns custom error messages
    /// </summary>
    /// <param name="task">The task to validate</param>
    /// <param name="template">The form template</param>
    /// <param name="formData">The form data</param>
    /// <param name="isFieldHidden">Optional predicate to check if a field is hidden by conditional logic</param>
    /// <returns>Dictionary of field IDs mapped to their custom error messages (or default messages)</returns>
    Dictionary<string, string> GetMissingRequiredFieldsWithMessages(DomainTask task, FormTemplate template, Dictionary<string, object> formData, Func<string, bool>? isFieldHidden = null);
}