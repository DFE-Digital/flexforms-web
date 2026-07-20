using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models.EventMapping;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace GovUK.Dfe.FlexForms.Infrastructure.Providers;

/// <summary>
/// Provides event field mapping configurations from JSON files
/// </summary>
public class EventMappingProvider(
    IConfiguration configuration,
    ILogger<EventMappingProvider> logger) : IEventMappingProvider
{
    private readonly string _basePath = configuration["EventMappings:BasePath"] ?? "EventMappings";

    /// <summary>
    /// Gets the event mapping configuration for a specific event type and template
    /// </summary>
    public async Task<EventFieldMapping?> GetMappingAsync(
        string templateId,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Construct the path to the mapping file
            // Format: EventMappings/{templateId}/{eventType}.json
            var filePath = Path.Combine(_basePath, templateId, $"{eventType}.json");

            if (!File.Exists(filePath))
            {
                logger.LogWarning(
                    "Event mapping file not found: {FilePath} (Template: {TemplateId}, Event: {EventType})",
                    filePath,
                    templateId,
                    eventType);
                return null;
            }

            logger.LogDebug("Loading event mapping from: {FilePath}", filePath);

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var mapping = JsonSerializer.Deserialize<EventFieldMapping>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (mapping == null)
            {
                logger.LogWarning("Failed to deserialize event mapping from: {FilePath}", filePath);
                return null;
            }

            logger.LogInformation(
                "Successfully loaded event mapping: {MappingId} for {EventType}",
                mapping.MappingId,
                eventType);

            return mapping;
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
}

