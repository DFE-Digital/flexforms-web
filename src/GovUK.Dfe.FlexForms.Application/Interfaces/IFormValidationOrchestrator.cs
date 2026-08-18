using GovUK.Dfe.FlexForms.Application.Validation;

namespace GovUK.Dfe.FlexForms.Application.Interfaces
{
    /// <summary>
    /// Handles validation logic for different form states and components
    /// </summary>
    public interface IFormValidationOrchestrator
    {
        /// <summary>
        /// Validates a single page
        /// </summary>
        FormValidationResult ValidatePage(Domain.Models.Page page, Dictionary<string, object> data, Domain.Models.FormTemplate? template = null);

        /// <summary>
        /// Validates a single task
        /// </summary>
        FormValidationResult ValidateTask(Domain.Models.Task task, Dictionary<string, object> data, Domain.Models.FormTemplate? template = null);

        /// <summary>
        /// Validates the entire application
        /// </summary>
        FormValidationResult ValidateApplication(Domain.Models.FormTemplate template, Dictionary<string, object> data);

        /// <summary>
        /// Validates a single field
        /// </summary>
        FormValidationResult ValidateField(Domain.Models.Field field, object value, string fieldKey);

        /// <summary>
        /// Validates a single field with full form data context for conditional validation
        /// </summary>
        FormValidationResult ValidateField(Domain.Models.Field field, object value, Dictionary<string, object>? formData, string fieldKey);
    }
}
