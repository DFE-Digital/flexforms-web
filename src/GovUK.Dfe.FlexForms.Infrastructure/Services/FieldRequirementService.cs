using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.Extensions.Logging;
using Task = GovUK.Dfe.FlexForms.Domain.Models.Task;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services;

/// <summary>
/// Service to determine if a field is required based on template policy and field configuration
/// </summary>
public class FieldRequirementService(ILogger<FieldRequirementService> logger) : IFieldRequirementService
{
    private readonly ILogger<FieldRequirementService> _logger = logger;
    private const string PolicyRequired = "required";
    private const string PolicyOptional = "optional";

    /// <summary>
    /// Determines if a field is required based on the template's default policy,
    /// field's Required property, and field's validation rules
    /// </summary>
    public bool IsFieldRequired(Field field, FormTemplate template)
    {
        // Priority 1: Check if field has explicit validation rule with type="required"
        if (field.Validations != null)
        {
            foreach (var validation in field.Validations)
            {
                if (string.Equals(validation.Type, "required", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        // Priority 2: Check field's Required property
        if (field.Required.HasValue)
        {
            return field.Required.Value;
        }

        // Priority 3: Use template's default policy
        var defaultPolicy = template.DefaultFieldRequirementPolicy;

        return string.Equals(defaultPolicy, PolicyRequired, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets all required fields for a task
    /// </summary>
    public List<string> GetRequiredFieldsForTask(Task task, FormTemplate template, Func<string, bool>? isFieldHidden = null)
    {
        var requiredFields = new List<string>();

        if (task?.Pages == null)
        {
            return requiredFields;
        }

        foreach (var page in task.Pages)
        {
            if (page?.Fields == null) continue;

            foreach (var field in page.Fields)
            {
                // Check if field is hidden by conditional logic
                var isHidden = isFieldHidden != null && isFieldHidden(field.FieldId);
                
                // Skip fields that are hidden by conditional logic
                if (isHidden)
                {
                    continue;
                }
                
                var isRequired = IsFieldRequired(field, template);
                
                if (isRequired)
                {
                    requiredFields.Add(field.FieldId);
                }
            }
        }

        return requiredFields;
    }

    /// <summary>
    /// Validates that all required fields in a task have values
    /// </summary>
    public List<string> GetMissingRequiredFields(Task task, FormTemplate template, Dictionary<string, object> formData, Func<string, bool>? isFieldHidden = null)
    {
        var missingFields = new List<string>();
        
        var requiredFields = GetRequiredFieldsForTask(task, template, isFieldHidden);

        foreach (var fieldId in requiredFields)
        {
            if (!formData.TryGetValue(fieldId, out var value) || IsFieldValueEmpty(value))
            {
                missingFields.Add(fieldId);
            }
        }
        
        return missingFields;
    }

    /// <summary>
    /// Validates that all required fields in a task have values and returns custom error messages
    /// </summary>
    public Dictionary<string, string> GetMissingRequiredFieldsWithMessages(Task task, FormTemplate template, Dictionary<string, object> formData, Func<string, bool>? isFieldHidden = null)
    {
        var missingFieldsWithMessages = new Dictionary<string, string>();
        
        if (task?.Pages == null)
        {
            return missingFieldsWithMessages;
        }

        foreach (var page in task.Pages)
        {
            if (page?.Fields == null) continue;

            foreach (var field in page.Fields)
            {
                // Check if field is hidden by conditional logic
                var isHidden = isFieldHidden != null && isFieldHidden(field.FieldId);
                
                // Skip fields that are hidden by conditional logic
                if (isHidden)
                {
                    continue;
                }
                
                var isRequired = IsFieldRequired(field, template);
                
                if (isRequired)
                {
                    // Check if field has a value
                    if (!formData.TryGetValue(field.FieldId, out var value) || IsFieldValueEmpty(value))
                    {
                        // Get custom error message from validation rule if it exists
                        var customMessage = GetCustomRequiredMessage(field);
                        
                        if (!string.IsNullOrEmpty(customMessage))
                        {
                            // Use custom message from validation rule
                            missingFieldsWithMessages[field.FieldId] = customMessage;
                        }
                        else
                        {
                            // Use default format: field label
                            var fieldLabel = field.Label?.Value ?? field.FieldId;
                            missingFieldsWithMessages[field.FieldId] = $"• {fieldLabel}";
                        }
                    }
                }
            }
        }

        return missingFieldsWithMessages;
    }

    /// <summary>
    /// Gets the custom error message from a field's required validation rule, if it exists
    /// </summary>
    private static string? GetCustomRequiredMessage(Field field)
    {
        if (field.Validations != null)
        {
            foreach (var validation in field.Validations)
            {
                if (string.Equals(validation.Type, "required", StringComparison.OrdinalIgnoreCase))
                {
                    return validation.Message;
                }
            }
        }
        
        return null;
    }

    /// <summary>
    /// Checks if a field value is considered empty
    /// </summary>
    private static bool IsFieldValueEmpty(object? value)
    {
        if (value == null) return true;
        
        var stringValue = value.ToString();
        if (string.IsNullOrWhiteSpace(stringValue)) return true;
        
        // Special handling for upload fields - check if it's an empty JSON array or has no files
        if (stringValue.TrimStart().StartsWith("["))
        {
            try
            {
                // Try to parse as JSON array to check if it's empty or has no valid files
                var jsonDoc = System.Text.Json.JsonDocument.Parse(stringValue);
                if (jsonDoc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    var arrayLength = jsonDoc.RootElement.GetArrayLength();
                    // Empty array means no files uploaded
                    return arrayLength == 0;
                }
            }
            catch
            {
                // If parsing fails, treat as non-empty string value
                return false;
            }
        }
        
        return false;
    }
}

