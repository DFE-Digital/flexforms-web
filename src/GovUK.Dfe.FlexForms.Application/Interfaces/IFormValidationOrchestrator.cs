using Microsoft.AspNetCore.Mvc.ModelBinding;

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
        /// <param name="page">The page to validate</param>
        /// <param name="data">The form data</param>
        /// <param name="modelState">The model state to add errors to</param>
        /// <param name="template">Optional template for field requirement policy</param>
        /// <returns>True if validation passes</returns>
        bool ValidatePage(Domain.Models.Page page, Dictionary<string, object> data, ModelStateDictionary modelState, Domain.Models.FormTemplate? template = null);
        
        /// <summary>
        /// Validates a single task
        /// </summary>
        /// <param name="task">The task to validate</param>
        /// <param name="data">The form data</param>
        /// <param name="modelState">The model state to add errors to</param>
        /// <param name="template">Optional template for field requirement policy</param>
        /// <returns>True if validation passes</returns>
        bool ValidateTask(Domain.Models.Task task, Dictionary<string, object> data, ModelStateDictionary modelState, Domain.Models.FormTemplate? template = null);
        
        /// <summary>
        /// Validates the entire application
        /// </summary>
        /// <param name="template">The form template</param>
        /// <param name="data">The form data</param>
        /// <param name="modelState">The model state to add errors to</param>
        /// <returns>True if validation passes</returns>
        bool ValidateApplication(Domain.Models.FormTemplate template, Dictionary<string, object> data, ModelStateDictionary modelState);
        
        /// <summary>
        /// Validates a single field
        /// </summary>
        /// <param name="field">The field to validate</param>
        /// <param name="value">The field value</param>
        /// <param name="modelState">The model state to add errors to</param>
        /// <param name="fieldKey">The field key for model state</param>
        /// <returns>True if validation passes</returns>
        bool ValidateField(Domain.Models.Field field, object value, ModelStateDictionary modelState, string fieldKey);
        
        /// <summary>
        /// Validates a single field with full form data context for conditional validation
        /// </summary>
        /// <param name="field">The field to validate</param>
        /// <param name="value">The field value</param>
        /// <param name="formData">The complete form data for conditional evaluation</param>
        /// <param name="modelState">The model state to add errors to</param>
        /// <param name="fieldKey">The field key for model state</param>
        /// <returns>True if validation passes</returns>
        bool ValidateField(Domain.Models.Field field, object value, Dictionary<string, object>? formData, ModelStateDictionary modelState, string fieldKey);
    }
}
