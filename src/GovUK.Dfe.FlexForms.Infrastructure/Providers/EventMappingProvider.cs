using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models.EventMapping;
using GovUK.Dfe.FlexForms.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace GovUK.Dfe.FlexForms.Infrastructure.Providers;

/// <summary>
/// Provides event field mapping configurations from TenantConfig (preferred) with disk file fallback.
/// Shape under category <c>EventMappings</c>: <c>{templateId}:{eventType}</c> nested objects.
/// </summary>
public class EventMappingProvider(
    IConfiguration hostConfiguration,
    IRequestAppConfiguration requestAppConfiguration,
    ILogger<EventMappingProvider> logger) : IEventMappingProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <inheritdoc />
    public async Task<EventFieldMapping?> GetMappingAsync(
        string templateId,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fromConfig = TryGetFromConfiguration(templateId, eventType);
            if (fromConfig != null)
            {
                logger.LogInformation(
                    "Loaded event mapping from TenantConfig/appsettings for template {TemplateId} and event {EventType} (MappingId: {MappingId})",
                    templateId,
                    eventType,
                    fromConfig.MappingId);
                return fromConfig;
            }

            return await TryGetFromDiskAsync(templateId, eventType, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error loading event mapping for template {TemplateId} and event {EventType}",
                templateId,
                eventType);
            throw;
        }
    }

    private EventFieldMapping? TryGetFromConfiguration(string templateId, string eventType)
    {
        var section = requestAppConfiguration.GetSection($"EventMappings:{templateId}:{eventType}");
        if (!section.Exists() || (!section.GetChildren().Any() && string.IsNullOrEmpty(section.Value)))
        {
            // Host-only path when request config has no overlay for this key
            section = hostConfiguration.GetSection($"EventMappings:{templateId}:{eventType}");
        }

        if (!section.Exists() || (!section.GetChildren().Any() && string.IsNullOrEmpty(section.Value)))
            return null;

        var json = ConfigurationSectionJson.ToJson(section);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<EventFieldMapping>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(
                ex,
                "Failed to deserialize EventMappings config for template {TemplateId} and event {EventType}",
                templateId,
                eventType);
            return null;
        }
    }

    private async Task<EventFieldMapping?> TryGetFromDiskAsync(
        string templateId,
        string eventType,
        CancellationToken cancellationToken)
    {
        var basePath = requestAppConfiguration["EventMappings:BasePath"]
            ?? hostConfiguration["EventMappings:BasePath"]
            ?? "EventMappings";

        var filePath = Path.Combine(basePath, templateId, $"{eventType}.json");

        if (!File.Exists(filePath))
        {
            logger.LogWarning(
                "Event mapping not found in TenantConfig or disk: {FilePath} (Template: {TemplateId}, Event: {EventType})",
                filePath,
                templateId,
                eventType);
            return null;
        }

        logger.LogDebug("Loading event mapping from disk: {FilePath}", filePath);

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var mapping = JsonSerializer.Deserialize<EventFieldMapping>(json, SerializerOptions);

        if (mapping == null)
        {
            logger.LogWarning("Failed to deserialize event mapping from: {FilePath}", filePath);
            return null;
        }

        logger.LogInformation(
            "Successfully loaded event mapping from disk: {MappingId} for {EventType}",
            mapping.MappingId,
            eventType);

        return mapping;
    }
}
