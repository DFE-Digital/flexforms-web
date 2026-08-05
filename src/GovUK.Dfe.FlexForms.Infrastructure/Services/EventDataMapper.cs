using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Domain.Models.EventMapping;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services;

/// <summary>
/// Maps form data to event models using the existing form engine services
/// Follows SOLID principles by delegating to specialized services
/// </summary>
public class EventDataMapper(
    IEventMappingProvider mappingProvider,
    ILogger<EventDataMapper> logger) : IEventDataMapper
{
    /// <summary>
    /// Maps accumulated form data to a specific event type using the configured mapping
    /// </summary>
    public async Task<TEvent> MapToEventAsync<TEvent>(
        Dictionary<string, object> formData,
        FormTemplate template,
        string mappingId,
        Guid applicationId,
        string applicationReference,
        CancellationToken cancellationToken = default) where TEvent : class
    {
        var eventType = typeof(TEvent).Name;
        var eventData = await BuildEventDataAsync(
            formData,
            template,
            eventType,
            mappingId,
            applicationId,
            applicationReference,
            cancellationToken);

        var json = JsonSerializer.Serialize(eventData);
        var eventObject = JsonSerializer.Deserialize<TEvent>(json);

        if (eventObject == null)
        {
            throw new InvalidOperationException($"Failed to deserialize event of type {eventType}");
        }

        logger.LogInformation(
            "Successfully mapped event {EventType} for application {ApplicationId}",
            eventType,
            applicationId);

        return eventObject;
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, object?>> MapToDictionaryAsync(
        Dictionary<string, object> formData,
        FormTemplate template,
        string eventTypeName,
        string mappingId,
        Guid applicationId,
        string applicationReference,
        CancellationToken cancellationToken = default)
    {
        var eventData = await BuildEventDataAsync(
            formData,
            template,
            eventTypeName,
            mappingId,
            applicationId,
            applicationReference,
            cancellationToken);

        return eventData.ToDictionary(
            kv => kv.Key,
            kv => (object?)kv.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, object>> BuildEventDataAsync(
        Dictionary<string, object> formData,
        FormTemplate template,
        string eventType,
        string mappingId,
        Guid applicationId,
        string applicationReference,
        CancellationToken cancellationToken)
    {
        logger.LogDebug(
            "Starting event mapping: {EventType} using mapping {MappingId} for application {ApplicationId}",
            eventType,
            mappingId,
            applicationId);

        var mapping = await mappingProvider.GetMappingAsync(template.TemplateId, eventType, cancellationToken);

        if (mapping == null)
        {
            throw new InvalidOperationException(
                $"No mapping found for event type '{eventType}' and template '{template.TemplateId}'");
        }

        var eventData = new Dictionary<string, object>();

        foreach (var (propertyName, fieldMapping) in mapping.FieldMappings)
        {
            try
            {
                var value = ExtractValue(fieldMapping, formData, template, applicationId, applicationReference);

                if (value == null || (value is string str && string.IsNullOrEmpty(str)))
                {
                    logger.LogTrace("Skipping property {PropertyName} - null or empty value", propertyName);
                    continue;
                }

                eventData[propertyName] = value;
                logger.LogTrace("Mapped property {PropertyName} = {Value}", propertyName, value);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Error extracting value for property {PropertyName} in event {EventType}",
                    propertyName,
                    eventType);
                throw;
            }
        }

        return eventData;
    }

    /// <summary>
    /// Extracts a value based on the field mapping configuration
    /// Delegates to specialized extraction methods based on source type
    /// </summary>
    private object ExtractValue(
        FieldMapping fieldMapping,
        Dictionary<string, object> formData,
        FormTemplate template,
        Guid applicationId,
        string applicationReference)
    {
        return fieldMapping.SourceType switch
        {
            FieldMappingSourceType.DirectField => 
                GetFieldValue(fieldMapping.SourceFieldId!, formData),
            
            FieldMappingSourceType.ComplexFieldProperty => 
                GetComplexFieldProperty(fieldMapping.SourceFieldId!, fieldMapping.NestedPath!, formData),
            
            FieldMappingSourceType.Collection => 
                GetCollectionValues(fieldMapping.CollectionMapping!, formData),
            
            FieldMappingSourceType.Computed => 
                ComputeValue(
                    fieldMapping.SourceFieldIds!, 
                    formData, 
                    fieldMapping.TransformationType!, 
                    fieldMapping.TransformationConfig, 
                    fieldMapping.DefaultValue),
            
            FieldMappingSourceType.Static => 
                ResolveStaticValue(fieldMapping.TransformationType, fieldMapping.DefaultValue),
            
            FieldMappingSourceType.Metadata => 
                GetMetadataValue(fieldMapping.SourceFieldId!, applicationId, applicationReference, fieldMapping.DefaultValue),
            
            _ => fieldMapping.DefaultValue ?? string.Empty
        };
    }

    /// <summary>
    /// Gets a field value directly from form data
    /// The form data has already been unwrapped by GetAccumulatedFormData
    /// </summary>
    private object GetFieldValue(string fieldId, Dictionary<string, object> formData)
    {
        if (!formData.TryGetValue(fieldId, out var value))
        {
            logger.LogDebug("Field {FieldId} not found in form data", fieldId);
            return string.Empty;
        }

        // Clean JSON elements if needed
        var cleaned = CleanValue(value);
        return cleaned ?? string.Empty;
    }

    /// <summary>
    /// Cleans a value from JsonElement format if needed
    /// </summary>
    private object? CleanValue(object? value)
    {
        if (value == null)
            return null;

        if (value is JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.String => jsonElement.GetString(),
                JsonValueKind.Number => jsonElement.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => jsonElement.ToString()
            };
        }

        return value;
    }

    /// <summary>
    /// Gets a property from a complex field (e.g., autocomplete field with nested data)
    /// </summary>
    private object GetComplexFieldProperty(string fieldId, string propertyPath, Dictionary<string, object> formData)
    {
        if (!formData.TryGetValue(fieldId, out var fieldValue))
        {
            return string.Empty;
        }

        try
        {
            var valueStr = fieldValue?.ToString();
            if (string.IsNullOrEmpty(valueStr))
            {
                return string.Empty;
            }

            // Decode HTML entities and parse JSON
            var decoded = System.Net.WebUtility.HtmlDecode(valueStr);
            var complexData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(decoded);

            if (complexData?.TryGetValue(propertyPath, out var propertyValue) == true)
            {
                if (propertyValue.ValueKind == JsonValueKind.String)
                {
                    var result = propertyValue.GetString() ?? string.Empty;
                    logger.LogDebug("Extracted string value: {Value}", result);
                    return result;
                }
                else if (propertyValue.ValueKind == JsonValueKind.Object)
                {
                    // Handle nested objects (e.g. gor: { name: "...", code: "..." })
                    // Try to extract the "name" property from nested object
                    if (propertyValue.TryGetProperty("name", out var nameProperty) && 
                        nameProperty.ValueKind == JsonValueKind.String)
                    {
                        var result = nameProperty.GetString() ?? string.Empty;
                        logger.LogDebug("Extracted nested name value: {Value}", result);
                        return result;
                    }
                    // If no name property, return the JSON representation
                    logger.LogDebug("Nested object has no 'name' property, returning JSON");
                    return propertyValue.ToString();
                }
                else
                {
                    return propertyValue.ToString();
                }
            }

            logger.LogDebug(
                "Property {PropertyPath} not found in complex field {FieldId}",
                propertyPath,
                fieldId);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(
                ex,
                "Failed to parse complex field {FieldId}.{PropertyPath}",
                fieldId,
                propertyPath);
        }

        return string.Empty;
    }

    /// <summary>
    /// Gets values from a collection flow (multi-collection or derived)
    /// </summary>
    private object GetCollectionValues(CollectionMapping collectionMapping, Dictionary<string, object> formData)
    {
        if (!formData.TryGetValue(collectionMapping.SourceCollectionFieldId, out var collectionValue))
        {
            logger.LogDebug(
                "Collection {CollectionId} not found in form data",
                collectionMapping.SourceCollectionFieldId);
            return collectionMapping.ItemMappings != null ? new List<object>() : string.Empty;
        }

        try
        {
            var decoded = System.Net.WebUtility.HtmlDecode(collectionValue?.ToString());
            if (string.IsNullOrEmpty(decoded))
            {
                return collectionMapping.ItemMappings != null ? new List<object>() : string.Empty;
            }

            var items = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(decoded);
            if (items == null || items.Count == 0)
            {
                return collectionMapping.ItemMappings != null ? new List<object>() : string.Empty;
            }

            logger.LogDebug(
                "Found {Count} items in collection {CollectionId}",
                items.Count,
                collectionMapping.SourceCollectionFieldId);

            // Extract single value from first item
            if (collectionMapping.ExtractFirst && !string.IsNullOrEmpty(collectionMapping.NestedPath))
            {
                var firstItem = items.First();
                return ExtractNestedProperty(firstItem, collectionMapping.NestedPath);
            }

            // Map all items to event structure
            if (collectionMapping.ItemMappings != null)
            {
                var mappedItems = new List<Dictionary<string, object>>();

                foreach (var item in items)
                {
                    var mappedItem = new Dictionary<string, object>();
                    var itemData = ConvertToFormData(item);

                    // Merge with original formData for cross-collection references
                    var mergedData = new Dictionary<string, object>(formData);
                    foreach (var kvp in itemData)
                    {
                        mergedData[kvp.Key] = kvp.Value;
                    }

                    foreach (var (propertyName, itemMapping) in collectionMapping.ItemMappings)
                    {
                        var value = ExtractValue(itemMapping, mergedData, null!, Guid.Empty, string.Empty);
                        
                        // Skip null/empty values for optional properties to allow deserializer to use defaults
                        if (value == null || (value is string str && string.IsNullOrEmpty(str)))
                        {
                            // Don't add the property - let the deserializer use null/default
                            continue;
                        }
                        
                        mappedItem[propertyName] = value;
                    }

                    mappedItems.Add(mappedItem);
                }

                logger.LogDebug(
                    "Mapped {Count} items from collection {CollectionId}",
                    mappedItems.Count,
                    collectionMapping.SourceCollectionFieldId);

                return mappedItems;
            }

            return items;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(
                ex,
                "Failed to parse collection {CollectionId}",
                collectionMapping.SourceCollectionFieldId);
            return new List<object>();
        }
    }

    /// <summary>
    /// Converts JsonElement dictionary to object dictionary for form data processing
    /// </summary>
    private Dictionary<string, object> ConvertToFormData(Dictionary<string, JsonElement> item)
    {
        var result = new Dictionary<string, object>();
        
        foreach (var (key, value) in item)
        {
            // Skip ID field
            if (key.Equals("id", StringComparison.OrdinalIgnoreCase))
                continue;

            result[key] = value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : value.ToString();
        }
        
        return result;
    }

    /// <summary>
    /// Extracts a nested property from a dictionary using dot notation
    /// Handles JSON strings and HTML entity decoding
    /// </summary>
    private object ExtractNestedProperty(Dictionary<string, JsonElement> source, string path)
    {
        var parts = path.Split('.');
        JsonElement current = default;
        var found = false;

        foreach (var part in parts)
        {
            if (!found && source.TryGetValue(part, out current))
            {
                found = true;
            }
            else if (found && current.ValueKind == JsonValueKind.Object && current.TryGetProperty(part, out current))
            {
                continue;
            }
            else if (found && current.ValueKind == JsonValueKind.String)
            {
                // Parse JSON string
                var decoded = System.Net.WebUtility.HtmlDecode(current.GetString());
                try
                {
                    var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(decoded!);
                    if (parsed?.TryGetValue(part, out current) == true)
                    {
                        continue;
                    }
                }
                catch (JsonException)
                {
                    logger.LogDebug("Failed to parse nested JSON at path {Path}", path);
                }
                return string.Empty;
            }
            else
            {
                logger.LogDebug("Path {Path} not found in source data", path);
                return string.Empty;
            }
        }

        return found && current.ValueKind == JsonValueKind.String
            ? current.GetString() ?? string.Empty
            : current.ToString();
    }

    /// <summary>
    /// Computes a value from multiple fields using a transformation
    /// </summary>
    private object ComputeValue(
        List<string> sourceFieldIds,
        Dictionary<string, object> formData,
        string transformationType,
        Dictionary<string, object>? config,
        object? defaultValue)
    {
        var values = sourceFieldIds
            .Select(id => GetFieldValue(id, formData))
            .Where(v => v != null && !string.IsNullOrEmpty(v.ToString()))
            .ToList();

        logger.LogDebug(
            "Computing value using {TransformationType} from {Count} source fields",
            transformationType,
            values.Count);

        return transformationType switch
        {
            "checkEquals" => values.FirstOrDefault()?.ToString() == config?["compareValue"]?.ToString(),
            "concatenate" => string.Join(" ", values),
            "sum" => values.Sum(v => Convert.ToDouble(v)),
            "count" => values.Count,
            "any" => values.Any(),
            _ => defaultValue ?? string.Empty
        };
    }

    /// <summary>
    /// Resolves a static or generated value
    /// </summary>
    private object ResolveStaticValue(string? transformationType, object? defaultValue)
    {
        return transformationType switch
        {
            "currentDateTime" => DateTime.UtcNow,
            "currentDate" => DateTime.UtcNow.Date,
            _ => defaultValue ?? string.Empty
        };
    }

    /// <summary>
    /// Gets metadata values (applicationId, applicationReference)
    /// </summary>
    private object GetMetadataValue(
        string metadataKey,
        Guid applicationId,
        string applicationReference,
        object? defaultValue)
    {
        return metadataKey switch
        {
            "applicationId" => applicationId.ToString(),
            "applicationReference" => applicationReference,
            _ => defaultValue ?? string.Empty
        };
    }
}

