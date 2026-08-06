using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services
{
    /// <summary>
    /// Implementation of derived collection flow service for generating forms based on other field values
    /// </summary>
    public class DerivedCollectionFlowService(
        ILogger<DerivedCollectionFlowService> logger) : IDerivedCollectionFlowService
    {
        public List<DerivedCollectionItem> GenerateItemsFromSourceField(
            string sourceFieldId, 
            Dictionary<string, object> formData,
            DerivedCollectionFlowConfiguration config)
        {
            var items = new List<DerivedCollectionItem>();
            
            logger.LogDebug("DerivedCollectionFlow: Looking for sourceFieldId '{SourceFieldId}' in form data", sourceFieldId);
            logger.LogDebug("DerivedCollectionFlow: Available form data keys: {Keys}", string.Join(", ", formData.Keys));
            
            if (!formData.TryGetValue(sourceFieldId, out var sourceValue))
            {
                logger.LogWarning("DerivedCollectionFlow: Source field '{SourceFieldId}' not found in form data", sourceFieldId);
                return items;
            }
                
            var sourceJson = sourceValue?.ToString() ?? "";
            logger.LogDebug("DerivedCollectionFlow: Source value type: {ValueType}, Value: {Value}", 
                sourceValue?.GetType().Name ?? "null", sourceValue);
                
            if (string.IsNullOrWhiteSpace(sourceJson))
            {
                logger.LogWarning("DerivedCollectionFlow: Source field '{SourceFieldId}' is empty", sourceFieldId);
                return items;
            }
            
            try
            {
                return config.SourceType?.ToLower() switch
                {
                    "autocomplete" => ProcessAutocompleteSource(sourceJson, config),
                    "checkboxes" => ProcessCheckboxSource(sourceJson, config),
                    "collection" => ProcessCollectionSource(sourceJson, config),
                    "select" => ProcessSelectSource(sourceJson, config),
                    _ => ProcessGenericSource(sourceJson, config) // Default fallback
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process source field '{SourceFieldId}' with type '{SourceType}'", 
                    sourceFieldId, config.SourceType);
                return items;
            }
        }
        
        public Dictionary<string, string> GetItemStatuses(string fieldId, Dictionary<string, object> formData)
        {
            var statuses = new Dictionary<string, string>();
            
            // Look for status fields with pattern: {fieldId}_status_{itemId}
            var statusPattern = $"{fieldId}_status_";
            
            foreach (var kvp in formData)
            {
                if (kvp.Key.StartsWith(statusPattern))
                {
                    var itemId = kvp.Key.Substring(statusPattern.Length);
                    var status = kvp.Value?.ToString() ?? "Not signed yet";
                    statuses[itemId] = status;
                }
            }
            
            return statuses;
        }
        
        public Dictionary<string, object> GetItemDeclarationData(
            string fieldId, 
            string itemId, 
            Dictionary<string, object> formData)
        {
            var declarationKey = $"{fieldId}_data_{itemId}";
            
            if (formData.TryGetValue(declarationKey, out var declarationValue))
            {
                var declarationJson = declarationValue?.ToString() ?? "{}";
                try
                {
                    return JsonSerializer.Deserialize<Dictionary<string, object>>(declarationJson) 
                           ?? new Dictionary<string, object>();
                }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex, "Failed to deserialize declaration data for item '{ItemId}'", itemId);
                }
            }
            
            return new Dictionary<string, object>();
        }
        
        public void SaveItemDeclaration(
            string fieldId, 
            string itemId, 
            Dictionary<string, object> declarationData, 
            string status, 
            Dictionary<string, object> formData)
        {
            // Save status
            var statusKey = $"{fieldId}_status_{itemId}";
            formData[statusKey] = status;
            
            // Save declaration data
            var declarationKey = $"{fieldId}_data_{itemId}";
            var declarationJson = JsonSerializer.Serialize(declarationData);
            formData[declarationKey] = declarationJson;
            
            logger.LogDebug("Saved declaration for item '{ItemId}' with status '{Status}'", itemId, status);
        }
        
        private List<DerivedCollectionItem> ProcessAutocompleteSource(string sourceJson, DerivedCollectionFlowConfiguration config)
        {
            var items = new List<DerivedCollectionItem>();
            
            logger.LogDebug("DerivedCollectionFlow: Processing autocomplete source JSON: {SourceJson}", sourceJson);
            
            try
            {
                // Try to parse as array of objects first: [{"name":"Trust A","id":"123"}, {...}]
                var autocompleteItems = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(sourceJson);
                
                logger.LogDebug("DerivedCollectionFlow: Parsed {Count} autocomplete items", autocompleteItems?.Count ?? 0);
                
                if (autocompleteItems != null)
                {
                    foreach (var item in autocompleteItems)
                    {
                        var displayName = GetDisplayName(item, config.ItemTitleBinding);
                        if (!string.IsNullOrWhiteSpace(displayName))
                        {
                            var itemId = GenerateItemId(displayName);
                            
                            items.Add(new DerivedCollectionItem
                            {
                                Id = itemId,
                                DisplayName = displayName,
                                Status = "Not signed yet",
                                PrefilledData = CreatePrefilledData(item, config),
                                SourceData = item
                            });
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Fallback: Handle as simple string array ["Trust A", "Trust B"]
                items = ProcessSimpleArraySource(sourceJson, config);
            }
            
            return items;
        }
        
        private List<DerivedCollectionItem> ProcessCheckboxSource(string sourceJson, DerivedCollectionFlowConfiguration config)
        {
            try
            {
                // Handle checkbox arrays: ["option1", "option2", "option3"]
                var selectedOptions = JsonSerializer.Deserialize<List<string>>(sourceJson) ?? new List<string>();
                
                return selectedOptions
                    .Where(option => !string.IsNullOrWhiteSpace(option))
                    .Select(option => new DerivedCollectionItem
                    {
                        Id = GenerateItemId(option),
                        DisplayName = option,
                        Status = "Not signed yet",
                        PrefilledData = new Dictionary<string, object> { [config.ItemTitleBinding] = option }
                    }).ToList();
            }
            catch (JsonException)
            {
                return ProcessSimpleArraySource(sourceJson, config);
            }
        }
        
        private List<DerivedCollectionItem> ProcessCollectionSource(string sourceJson, DerivedCollectionFlowConfiguration config)
        {
            logger.LogDebug("DerivedCollectionFlow: Processing collection source JSON: {SourceJson}", sourceJson);
            
            try
            {
                // Handle existing collection flow items
                var collectionItems = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(sourceJson);
                
                logger.LogDebug("DerivedCollectionFlow: Parsed {Count} collection items", collectionItems?.Count ?? 0);
                
                if (collectionItems != null)
                {
                    return collectionItems.SelectMany(item =>
                    {
                        logger.LogDebug("DerivedCollectionFlow: Processing collection item: {Item}", JsonSerializer.Serialize(item));
                        
                        // Collection items may contain nested field data
                        // Look for the actual autocomplete data within each collection item
                        var derivedItems = new List<DerivedCollectionItem>();
                        
                        foreach (var kvp in item)
                        {
                            // Skip metadata fields
                            if (kvp.Key == "id" || kvp.Key == "_metadata" || kvp.Key.StartsWith("Data[") || kvp.Key.StartsWith("Data_")) continue;
                            
                            // Try to extract autocomplete data from the field value
                            if (kvp.Value != null)
                            {
                                var fieldValue = HttpUtility.HtmlDecode(kvp.Value.ToString());
                                if (!string.IsNullOrEmpty(fieldValue) && fieldValue.StartsWith("{"))
                                {
                                    try
                                    {
                                        // Parse the nested autocomplete object
                                        var autocompleteData = JsonSerializer.Deserialize<Dictionary<string, object>>(fieldValue);
                                        if (autocompleteData != null)
                                        {
                                            var displayName = GetDisplayName(autocompleteData, config.ItemTitleBinding);
                                            logger.LogDebug("DerivedCollectionFlow: Extracted display name '{DisplayName}' from nested data using binding '{Binding}'", displayName, config.ItemTitleBinding);
                                            
                                            var itemId = autocompleteData.TryGetValue("id", out var id) ? id?.ToString() : GenerateItemId(displayName);
                                            
                                            derivedItems.Add(new DerivedCollectionItem
                                            {
                                                Id = itemId ?? GenerateItemId(displayName),
                                                DisplayName = displayName,
                                                Status = "Not signed yet",
                                                PrefilledData = CreatePrefilledData(autocompleteData, config),
                                                SourceData = autocompleteData
                                            });
                                        }
                                    }
                                    catch (JsonException ex)
                                    {
                                        logger.LogWarning(ex, "Failed to parse nested autocomplete data: {FieldValue}", fieldValue);
                                    }
                                }
                            }
                        }
                        
                        return derivedItems;
                    }).ToList();
                }
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to parse collection source data as JSON");
            }
            
            return new List<DerivedCollectionItem>();
        }
        
        private List<DerivedCollectionItem> ProcessSelectSource(string sourceJson, DerivedCollectionFlowConfiguration config)
        {
            // Handle single select value or comma-separated values
            var values = sourceJson.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                   .Select(v => v.Trim())
                                   .Where(v => !string.IsNullOrWhiteSpace(v));
            
            return values.Select(value => new DerivedCollectionItem
            {
                Id = GenerateItemId(value),
                DisplayName = value,
                Status = "Not signed yet",
                PrefilledData = new Dictionary<string, object> { [config.ItemTitleBinding] = value }
            }).ToList();
        }
        
        private List<DerivedCollectionItem> ProcessGenericSource(string sourceJson, DerivedCollectionFlowConfiguration config)
        {
            // Generic fallback - try various common formats
            try
            {
                // Try as array first
                return ProcessSimpleArraySource(sourceJson, config);
            }
            catch
            {
                // Try as single value
                if (!string.IsNullOrWhiteSpace(sourceJson))
                {
                    return new List<DerivedCollectionItem>
                    {
                        new DerivedCollectionItem
                        {
                            Id = GenerateItemId(sourceJson),
                            DisplayName = sourceJson,
                            Status = "Not signed yet",
                            PrefilledData = new Dictionary<string, object> { [config.ItemTitleBinding] = sourceJson }
                        }
                    };
                }
            }
            
            return new List<DerivedCollectionItem>();
        }
        
        private List<DerivedCollectionItem> ProcessSimpleArraySource(string sourceJson, DerivedCollectionFlowConfiguration config)
        {
            try
            {
                var simpleValues = JsonSerializer.Deserialize<List<string>>(sourceJson) ?? new List<string>();
                
                return simpleValues
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => new DerivedCollectionItem
                    {
                        Id = GenerateItemId(value),
                        DisplayName = value,
                        Status = "Not signed yet",
                        PrefilledData = new Dictionary<string, object> { [config.ItemTitleBinding] = value }
                    }).ToList();
            }
            catch (JsonException)
            {
                // Try parsing as comma-separated string
                var values = sourceJson.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                       .Select(v => v.Trim())
                                       .Where(v => !string.IsNullOrWhiteSpace(v));
                
                return values.Select(value => new DerivedCollectionItem
                {
                    Id = GenerateItemId(value),
                    DisplayName = value,
                    Status = "Not signed yet",
                    PrefilledData = new Dictionary<string, object> { [config.ItemTitleBinding] = value }
                }).ToList();
            }
        }
        
        private string GetDisplayName(Dictionary<string, object> item, string itemTitleBinding)
        {
            if (item.TryGetValue(itemTitleBinding, out var value))
            {
                return value?.ToString() ?? "";
            }
            
            // Fallback: try common field names
            var commonFields = new[] { "name", "title", "label", "text", "value" };
            foreach (var field in commonFields)
            {
                if (item.TryGetValue(field, out var fallbackValue))
                {
                    return fallbackValue?.ToString() ?? "";
                }
            }
            
            // Last resort: use first available value
            return item.Values.FirstOrDefault()?.ToString() ?? "";
        }
        
        private Dictionary<string, object> CreatePrefilledData(Dictionary<string, object> sourceItem, DerivedCollectionFlowConfiguration config)
        {
            var prefilledData = new Dictionary<string, object>();
            
            // Always include the title binding
            var displayName = GetDisplayName(sourceItem, config.ItemTitleBinding);
            prefilledData[config.ItemTitleBinding] = displayName;
            
            // Copy over any matching fields from source data
            foreach (var kvp in sourceItem)
            {
                prefilledData[kvp.Key] = kvp.Value;
            }
            
            return prefilledData;
        }
        
        private string GenerateItemId(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return Guid.NewGuid().ToString("N")[..8];
                
            // Convert to lowercase, replace spaces and special chars with hyphens
            var itemId = Regex.Replace(displayName.ToLowerInvariant(), @"[^a-z0-9]+", "-")
                             .Trim('-');
            
            // Ensure it's not empty and not too long
            if (string.IsNullOrEmpty(itemId))
                itemId = Guid.NewGuid().ToString("N")[..8];
            else if (itemId.Length > 50)
                itemId = itemId[..50].TrimEnd('-');
                
            return itemId;
        }
    }
}
