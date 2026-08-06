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

            // Admin UI often saves under the API template GUID, while form JSON may use a
            // legacy schema TemplateId (e.g. form-001). Search sibling keys for the same event.
            var fromSibling = TryGetFromAnyTemplateKey(eventType, preferredTemplateId: templateId);
            if (fromSibling != null)
                return fromSibling;

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

        return DeserializeMapping(section, templateId, eventType);
    }

    /// <summary>
    /// Finds <paramref name="eventType"/> under any EventMappings template key when the
    /// runtime schema TemplateId does not match the Admin-saved API GUID key.
    /// </summary>
    private EventFieldMapping? TryGetFromAnyTemplateKey(string eventType, string preferredTemplateId)
    {
        var matches = new List<(string TemplateKey, EventFieldMapping Mapping)>();

        CollectEventMatches(requestAppConfiguration.GetSection("EventMappings"), eventType, matches);
        if (matches.Count == 0)
            CollectEventMatches(hostConfiguration.GetSection("EventMappings"), eventType, matches);

        if (matches.Count == 0)
            return null;

        // Prefer a key that is a Guid when the preferred key looks like a legacy string (or vice versa).
        var preferredIsGuid = Guid.TryParse(preferredTemplateId, out _);
        var preferredMatch = matches.FirstOrDefault(m =>
            Guid.TryParse(m.TemplateKey, out _) != preferredIsGuid
            || string.Equals(m.TemplateKey, preferredTemplateId, StringComparison.OrdinalIgnoreCase));

        var chosen = preferredMatch.Mapping is not null
            ? preferredMatch
            : matches[0];

        if (matches.Count > 1)
        {
            logger.LogWarning(
                "Multiple EventMappings entries found for event {EventType} under templates [{Keys}]. Using template key {ChosenKey}.",
                eventType,
                string.Join(", ", matches.Select(m => m.TemplateKey)),
                chosen.TemplateKey);
        }
        else
        {
            logger.LogInformation(
                "Resolved event mapping for {EventType} under TenantConfig template key {TemplateKey} (requested {RequestedTemplateId})",
                eventType,
                chosen.TemplateKey,
                preferredTemplateId);
        }

        return chosen.Mapping;
    }

    private void CollectEventMatches(
        IConfigurationSection eventMappingsRoot,
        string eventType,
        List<(string TemplateKey, EventFieldMapping Mapping)> matches)
    {
        if (!eventMappingsRoot.Exists())
            return;

        foreach (var templateSection in eventMappingsRoot.GetChildren())
        {
            // Skip non-template keys such as BasePath
            if (string.Equals(templateSection.Key, "BasePath", StringComparison.OrdinalIgnoreCase))
                continue;

            var eventSection = templateSection.GetSection(eventType);
            if (!eventSection.Exists() || (!eventSection.GetChildren().Any() && string.IsNullOrEmpty(eventSection.Value)))
                continue;

            var mapping = DeserializeMapping(eventSection, templateSection.Key, eventType);
            if (mapping is null)
                continue;

            if (matches.Any(m => string.Equals(m.TemplateKey, templateSection.Key, StringComparison.OrdinalIgnoreCase)))
                continue;

            matches.Add((templateSection.Key, mapping));
        }
    }

    private EventFieldMapping? DeserializeMapping(IConfigurationSection section, string templateId, string eventType)
    {
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
