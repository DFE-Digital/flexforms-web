using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Application.Interfaces
{
    /// <summary>
    /// Service for formatting field values for display purposes
    /// </summary>
    public interface IFieldFormattingService
    {
        /// <summary>
        /// Gets the raw field value from form data
        /// </summary>
        string GetFieldValue(string fieldId, Dictionary<string, object> formData);

        /// <summary>
        /// Gets formatted field value for display (handles autocomplete formatting)
        /// </summary>
        string GetFormattedFieldValue(string fieldId, Dictionary<string, object> formData);

        /// <summary>
        /// Gets formatted field values as a list (for multi-select fields)
        /// </summary>
        List<string> GetFormattedFieldValues(string fieldId, Dictionary<string, object> formData);

        /// <summary>
        /// Gets the label for field items from template configuration
        /// </summary>
        string GetFieldItemLabel(string fieldId, FormTemplate template);

        /// <summary>
        /// Checks if a field allows multiple selections
        /// </summary>
        bool IsFieldAllowMultiple(string fieldId, FormTemplate template);

        /// <summary>
        /// Checks if a field has any value
        /// </summary>
        bool HasFieldValue(string fieldId, Dictionary<string, object> formData);
    }
} 