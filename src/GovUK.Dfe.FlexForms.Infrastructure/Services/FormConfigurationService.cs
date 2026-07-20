using GovUK.Dfe.FlexForms.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services;

/// <summary>
/// Provides form settings from the effective request configuration.
/// </summary>
public sealed class FormConfigurationService(
    IRequestAppConfiguration requestConfiguration,
    ILogger<FormConfigurationService> logger) : IFormConfigurationService
{
    /// <inheritdoc />
    public FormConfiguration GetFormConfiguration(string templateId)
    {
        var configuration = requestConfiguration.Current;
        var config = new FormConfiguration
        {
            TemplateId = templateId,
            TemplateName = templateId,
            AllowPartialSaving = configuration.GetValue("FormEngine:AllowPartialSaving", true),
            RequireAllTasksCompleted = configuration.GetValue("FormEngine:RequireAllTasksCompleted", false),
            MaxFileUploadSize = configuration.GetValue("FormEngine:MaxFileUploadSize", 10 * 1024 * 1024),
            AllowedFileTypes = configuration.GetSection("FormEngine:AllowedFileTypes").Get<string[]>() ?? [".pdf", ".doc", ".docx"]
        };

        logger.LogDebug("Retrieved form configuration for template {TemplateId}", templateId);
        return config;
    }

    /// <inheritdoc />
    public FieldConfiguration GetFieldConfiguration(string fieldType)
    {
        var configuration = requestConfiguration.Current;
        var config = new FieldConfiguration
        {
            FieldType = fieldType,
            IsRequired = configuration.GetValue($"FormEngine:FieldTypes:{fieldType}:IsRequired", false),
            MaxLength = configuration.GetValue($"FormEngine:FieldTypes:{fieldType}:MaxLength", 0),
            DefaultValue = configuration.GetValue($"FormEngine:FieldTypes:{fieldType}:DefaultValue", string.Empty),
            ValidationRules = configuration.GetSection($"FormEngine:FieldTypes:{fieldType}:ValidationRules").Get<string[]>() ?? []
        };

        logger.LogDebug("Retrieved field configuration for type {FieldType}", fieldType);
        return config;
    }

    /// <inheritdoc />
    public ValidationConfiguration GetValidationConfiguration(string validationType)
    {
        var configuration = requestConfiguration.Current;
        var config = new ValidationConfiguration
        {
            ValidationType = validationType,
            ErrorMessage = configuration.GetValue($"FormEngine:ValidationTypes:{validationType}:ErrorMessage", "Validation failed"),
            Rule = configuration.GetValue<object>($"FormEngine:ValidationTypes:{validationType}:Rule", string.Empty),
            IsConditional = configuration.GetValue($"FormEngine:ValidationTypes:{validationType}:IsConditional", false)
        };

        logger.LogDebug("Retrieved validation configuration for type {ValidationType}", validationType);
        return config;
    }

    /// <inheritdoc />
    public FormSettings GetDefaultFormSettings()
    {
        var configuration = requestConfiguration.Current;
        var settings = new FormSettings
        {
            EnableAutoSave = configuration.GetValue("FormEngine:EnableAutoSave", true),
            AutoSaveInterval = configuration.GetValue("FormEngine:AutoSaveInterval", 30000),
            ShowProgressIndicator = configuration.GetValue("FormEngine:ShowProgressIndicator", true),
            EnableFieldValidation = configuration.GetValue("FormEngine:EnableFieldValidation", true),
            DefaultDateFormat = configuration.GetValue("FormEngine:DefaultDateFormat", "dd/MM/yyyy")
        };

        logger.LogDebug("Retrieved default form settings");
        return settings;
    }
}
