namespace GovUK.Dfe.FlexForms.Application.Interfaces
{
    /// <summary>
    /// Manages form configuration and settings
    /// </summary>
    public interface IFormConfigurationService
    {
        /// <summary>
        /// Gets the configuration for a specific form template
        /// </summary>
        /// <param name="templateId">The template ID</param>
        /// <returns>The form configuration</returns>
        FormConfiguration GetFormConfiguration(string templateId);
        
        /// <summary>
        /// Gets the configuration for a specific field type
        /// </summary>
        /// <param name="fieldType">The field type</param>
        /// <returns>The field configuration</returns>
        FieldConfiguration GetFieldConfiguration(string fieldType);
        
        /// <summary>
        /// Gets the configuration for a specific validation type
        /// </summary>
        /// <param name="validationType">The validation type</param>
        /// <returns>The validation configuration</returns>
        ValidationConfiguration GetValidationConfiguration(string validationType);
        
        /// <summary>
        /// Gets the default form settings
        /// </summary>
        /// <returns>The default form settings</returns>
        FormSettings GetDefaultFormSettings();
    }

    /// <summary>
    /// Configuration for a form template
    /// </summary>
    public class FormConfiguration
    {
        public string TemplateId { get; set; } = string.Empty;
        public string TemplateName { get; set; } = string.Empty;
        public bool AllowPartialSaving { get; set; } = true;
        public bool RequireAllTasksCompleted { get; set; } = false;
        public int MaxFileUploadSize { get; set; } = 10 * 1024 * 1024; // 10MB
        public string[] AllowedFileTypes { get; set; } = { ".pdf", ".doc", ".docx" };
    }

    /// <summary>
    /// Configuration for a field type
    /// </summary>
    public class FieldConfiguration
    {
        public string FieldType { get; set; } = string.Empty;
        public bool IsRequired { get; set; } = false;
        public int MaxLength { get; set; } = 0;
        public string DefaultValue { get; set; } = string.Empty;
        public string[] ValidationRules { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Configuration for a validation type
    /// </summary>
    public class ValidationConfiguration
    {
        public string ValidationType { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public object Rule { get; set; } = string.Empty;
        public bool IsConditional { get; set; } = false;
    }

    /// <summary>
    /// Default form settings
    /// </summary>
    public class FormSettings
    {
        public bool EnableAutoSave { get; set; } = true;
        public int AutoSaveInterval { get; set; } = 30000; // 30 seconds
        public bool ShowProgressIndicator { get; set; } = true;
        public bool EnableFieldValidation { get; set; } = true;
        public string DefaultDateFormat { get; set; } = "dd/MM/yyyy";
    }
}
