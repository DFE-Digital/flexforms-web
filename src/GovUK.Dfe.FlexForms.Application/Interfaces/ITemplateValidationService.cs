using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Application.Interfaces;

/// <summary>
/// Service for validating form template JSON against domain models
/// </summary>
public interface ITemplateValidationService
{
    /// <summary>
    /// Validates a JSON string against the FormTemplate domain model
    /// </summary>
    /// <param name="jsonString">The JSON string to validate</param>
    /// <returns>A tuple containing validation success status and a list of error messages</returns>
    (bool IsValid, List<string> Errors) ValidateTemplateJson(string jsonString);
    
    /// <summary>
    /// Attempts to parse and validate a JSON string as a FormTemplate
    /// </summary>
    /// <param name="jsonString">The JSON string to parse</param>
    /// <returns>A tuple containing the parsed template (or null) and a list of error messages</returns>
    (FormTemplate? Template, List<string> Errors) TryParseTemplate(string jsonString);
}

