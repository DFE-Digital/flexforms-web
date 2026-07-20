using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services;

/// <summary>
/// Loads complex field configuration from the effective request configuration.
/// </summary>
public sealed class ComplexFieldConfigurationService(
    IRequestAppConfiguration requestConfiguration,
    ILogger<ComplexFieldConfigurationService> logger) : IComplexFieldConfigurationService
{
    /// <inheritdoc />
    public ComplexFieldConfiguration GetConfiguration(string complexFieldId)
    {
        var configuration = requestConfiguration.Current;

        // First try the new structure (array of objects with Id property)
        var complexFieldsSection = configuration.GetSection("FormEngine:ComplexFields");
        if (complexFieldsSection.Exists())
        {
            var configurations = complexFieldsSection.Get<List<ComplexFieldConfiguration>>();
            if (configurations != null)
            {
                var config = SelectBestConfiguration(configurations, complexFieldId);
                if (config != null)
                {
                    NormalizeFieldType(config);
                    ApplySharedApiKeyFallback(config, configurations, configuration);
                    logger.LogDebug(
                        "Loaded complex field configuration for {ComplexFieldId}: FieldType={FieldType}, Endpoint={Endpoint}, AllowMultiple={AllowMultiple}, MinLength={MinLength}, HasApiKey={HasApiKey}",
                        complexFieldId, config.FieldType, config.ApiEndpoint, config.AllowMultiple, config.MinLength, !string.IsNullOrEmpty(config.ApiKey));
                    return config;
                }
            }
        }

        // Fallback to old structure (direct key lookup)
        var configSection = configuration.GetSection($"FormEngine:ComplexFields:{complexFieldId}");

        if (!configSection.Exists())
        {
            logger.LogWarning("Complex field configuration not found for ID: {ComplexFieldId}", complexFieldId);
            var missing = new ComplexFieldConfiguration { Id = complexFieldId };
            NormalizeFieldType(missing);
            return missing;
        }

        var fieldConfiguration = new ComplexFieldConfiguration
        {
            Id = complexFieldId,
            ApiEndpoint = configSection["ApiEndpoint"] ?? string.Empty,
            ApiKey = configSection["ApiKey"] ?? string.Empty,
            FieldType = configSection["FieldType"] ?? string.Empty,
            AllowMultiple = bool.TryParse(configSection["AllowMultiple"], out var allowMultiple) && allowMultiple,
            MinLength = int.TryParse(configSection["MinLength"], out var minLength) ? minLength : 3,
            Placeholder = configSection["Placeholder"] ?? "Start typing to search...",
            MaxSelections = int.TryParse(configSection["MaxSelections"], out var maxSelections) ? maxSelections : 0,
            Label = configSection["Label"] ?? "Item"
        };

        NormalizeFieldType(fieldConfiguration);

        foreach (var child in configSection.GetChildren())
        {
            if (!new[] { "ApiEndpoint", "ApiKey", "FieldType", "AllowMultiple", "MinLength", "Placeholder", "MaxSelections", "Label" }.Contains(child.Key))
            {
                fieldConfiguration.AdditionalProperties[child.Key] = child.Value ?? "";
            }
        }

        if (string.IsNullOrEmpty(fieldConfiguration.ApiKey))
        {
            var allConfigurations = complexFieldsSection.Exists()
                ? complexFieldsSection.Get<List<ComplexFieldConfiguration>>()
                : null;
            if (allConfigurations != null)
            {
                ApplySharedApiKeyFallback(fieldConfiguration, allConfigurations, configuration);
            }
        }

        logger.LogDebug(
            "Loaded complex field configuration for {ComplexFieldId}: FieldType={FieldType}, Endpoint={Endpoint}, AllowMultiple={AllowMultiple}, MinLength={MinLength}, HasApiKey={HasApiKey}",
            complexFieldId, fieldConfiguration.FieldType, fieldConfiguration.ApiEndpoint, fieldConfiguration.AllowMultiple, fieldConfiguration.MinLength, !string.IsNullOrEmpty(fieldConfiguration.ApiKey));

        return fieldConfiguration;
    }

    /// <inheritdoc />
    public bool HasConfiguration(string complexFieldId)
    {
        var configuration = requestConfiguration.Current;
        var complexFieldsSection = configuration.GetSection("FormEngine:ComplexFields");
        if (complexFieldsSection.Exists())
        {
            var configurations = complexFieldsSection.Get<List<ComplexFieldConfiguration>>();
            if (configurations != null)
            {
                return configurations.Any(c => c.Id == complexFieldId);
            }
        }

        var configSection = configuration.GetSection($"FormEngine:ComplexFields:{complexFieldId}");
        return configSection.Exists();
    }

    /// <summary>
    /// Host + tenant configuration merge can leave the same complex-field Id at multiple indexes
    /// (e.g. tenant entry without FieldType and host baseline with FieldType=upload). Prefer the
    /// richest definition so upload fields are not downgraded to autocomplete.
    /// </summary>
    private static ComplexFieldConfiguration? SelectBestConfiguration(
        IEnumerable<ComplexFieldConfiguration> configurations,
        string complexFieldId)
    {
        return configurations
            .Where(c => string.Equals(c.Id, complexFieldId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(ScoreConfiguration)
            .FirstOrDefault();
    }

    private static int ScoreConfiguration(ComplexFieldConfiguration config)
    {
        var score = 0;

        if (!string.IsNullOrWhiteSpace(config.FieldType))
        {
            score += 10;
            if (config.FieldType.Equals("upload", StringComparison.OrdinalIgnoreCase))
            {
                score += 20;
            }
            else if (config.FieldType.Equals("composite", StringComparison.OrdinalIgnoreCase))
            {
                score += 15;
            }
        }

        if (!string.IsNullOrWhiteSpace(config.ApiEndpoint))
        {
            score += 5;
        }

        if (!string.IsNullOrWhiteSpace(config.ApiKey))
        {
            score += 2;
        }

        if (config.AllowMultiple)
        {
            score += 1;
        }

        return score;
    }

    /// <summary>
    /// Ensures known upload fields keep FieldType=upload when config omitted it
    /// (defaults / incomplete tenant settings previously rendered autocomplete search).
    /// </summary>
    private static void NormalizeFieldType(ComplexFieldConfiguration config)
    {
        if (string.Equals(config.Id, "UploadDocumentsComplexField", StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(config.FieldType)
                || (config.FieldType.Equals("autocomplete", StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrWhiteSpace(config.ApiEndpoint))))
        {
            config.FieldType = "upload";
            return;
        }

        if (string.IsNullOrWhiteSpace(config.FieldType))
        {
            config.FieldType = "autocomplete";
        }
    }

    /// <summary>
    /// Reuses the Academies API key from another complex field when this field has none configured.
    /// </summary>
    private static void ApplySharedApiKeyFallback(
        ComplexFieldConfiguration config,
        List<ComplexFieldConfiguration> allConfigurations,
        IConfiguration configuration)
    {
        if (!string.IsNullOrEmpty(config.ApiKey))
        {
            return;
        }

        config.ApiKey = allConfigurations
            .Where(c => c.Id != config.Id && !string.IsNullOrEmpty(c.ApiKey))
            .Select(c => c.ApiKey)
            .FirstOrDefault()
            ?? configuration["FormEngine:AcademiesApiKey"]
            ?? string.Empty;
    }
}
