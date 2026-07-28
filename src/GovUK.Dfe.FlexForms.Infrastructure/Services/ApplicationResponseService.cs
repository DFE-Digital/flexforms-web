using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Domain.Caching;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services;

public class ApplicationResponseService(
    IApplicationsClient applicationsClient,
    IConnectionMultiplexer redis,
    IFormTemplateProvider formTemplateProvider,
    ILogger<ApplicationResponseService> logger)
    : IApplicationResponseService
{
    private const string SessionKeyFormData = "AccumulatedFormData";

    public async Task SaveApplicationResponseAsync(Guid applicationId, Dictionary<string, object> formData, ISession session, CancellationToken cancellationToken = default)
    {
        try
        {            
            // Accumulate the new data with existing data (infected files filtered by blacklist)
            AccumulateFormData(formData, session);
            
            // Get all accumulated data
            var allFormData = GetAccumulatedFormData(session);
            
            var taskStatusData = GetTaskStatusFromSession(applicationId, session);

            var template = await TryGetTemplateFromSessionAsync(session, cancellationToken);
            
            var responseJson = TransformToResponseJson(allFormData, taskStatusData, template);
            
            var encodedResponse = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(responseJson));

            var request = new AddApplicationResponseRequest { ResponseBody = encodedResponse };
            await applicationsClient.AddApplicationResponseAsync(applicationId, request, cancellationToken);
            
            // Update application status to InProgress when any data is saved
            // This ensures the dashboard shows the correct status
            await EnsureApplicationStatusIsInProgress(applicationId, allFormData, session, cancellationToken);
            
            logger.LogInformation("Successfully saved application response for {ApplicationId}", applicationId);
        }
        catch (ExternalApplicationsException ex) when (ex.StatusCode == 200)
        {
            // Handle the case where API returns 200 instead of 201
            logger.LogInformation("Application response saved successfully with status 200 for {ApplicationId}", applicationId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save application response for {ApplicationId}", applicationId);
            throw;
        }
    }

    private async Task EnsureApplicationStatusIsInProgress(Guid applicationId, Dictionary<string, object> allFormData, ISession session, CancellationToken cancellationToken)
    {
        try
        {
            // Check if any form fields have data
            var hasAnyData = allFormData.Any(kvp => !string.IsNullOrWhiteSpace(kvp.Value?.ToString()));
            
            if (hasAnyData)
            {
                // Get current application status from session
                var statusKey = $"ApplicationStatus_{applicationId}";
                var currentStatus = session.GetString(statusKey);
                
                // Only update if not already submitted
                if (string.IsNullOrEmpty(currentStatus) || currentStatus.Equals("InProgress", StringComparison.OrdinalIgnoreCase))
                {
                    // Update session status to InProgress
                    session.SetString(statusKey, "InProgress");
                    logger.LogInformation("Updated application {ApplicationId} status to InProgress due to form data being saved", applicationId);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update application status for {ApplicationId}, continuing with save operation", applicationId);
            // Don't throw - this is not critical to the save operation
        }
    }

    public void AccumulateFormData(Dictionary<string, object> newData, ISession session)
    {
        // Get existing data (infected files will be filtered by blacklist)
        var existingData = GetAccumulatedFormData(session);
        
        foreach (var kvp in newData)
        {
            var normalizedFieldName = NormalizeFieldName(kvp.Key);
            var fieldNameToUse = normalizedFieldName;
            
            logger.LogDebug("Normalizing field name: '{OriginalKey}' -> '{NormalizedKey}'", kvp.Key, fieldNameToUse);
            
            existingData[fieldNameToUse] = kvp.Value;
            
            var alternativeKeys = existingData.Keys
                .Where(key => key != fieldNameToUse && AreEquivalentFieldNames(key, fieldNameToUse))
                .ToList();
            
            foreach (var altKey in alternativeKeys)
            {
                logger.LogDebug("Removing duplicate field entry: {OldKey} in favor of {NewKey}", altKey, fieldNameToUse);
                existingData.Remove(altKey);
            }
        }
        
        var jsonString = JsonSerializer.Serialize(existingData);
        session.SetString(SessionKeyFormData, jsonString);
    }

    private bool AreEquivalentFieldNames(string fieldName1, string fieldName2)
    {
        var normalized1 = NormalizeFieldName(fieldName1);
        var normalized2 = NormalizeFieldName(fieldName2);
        
        return string.Equals(normalized1, normalized2, StringComparison.OrdinalIgnoreCase);
    }

    private string NormalizeFieldName(string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName))
            return fieldName;
            
        if (fieldName.StartsWith("Data_", StringComparison.OrdinalIgnoreCase))
        {
            return fieldName.Substring(5);
        }
        
        return fieldName;
    }

    /// <summary>
    /// Gets accumulated form data with infected file filtering
    /// </summary>
    public Dictionary<string, object> GetAccumulatedFormData(ISession session)
    {
        var jsonString = session.GetString(SessionKeyFormData);
        
        if (string.IsNullOrEmpty(jsonString))
        {
            return new Dictionary<string, object>();
        }
        
        try
        {
            var rawData = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString) 
                         ?? new Dictionary<string, object>();
            
            // Get applicationId from session for filename-based blacklist checking
            var applicationId = session.GetString("ApplicationId");
            
            // Filter out any infected files from the data
            var filteredData = FilterInfectedFilesFromData(rawData, applicationId);
            
            var cleanedData = new Dictionary<string, object>();
            foreach (var kvp in filteredData)
            {
                cleanedData[kvp.Key] = CleanFormValue(kvp.Value);
            }
            
            return cleanedData;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize accumulated form data from session. Starting fresh.");
            return new Dictionary<string, object>();
        }
    }
    
    private object CleanFormValue(object value)
    {
        if (value == null)
            return string.Empty;

        if (value is JsonElement jsonElement)
        {
            switch (jsonElement.ValueKind)
            {
                case JsonValueKind.String:
                    return jsonElement.GetString() ?? string.Empty;
                case JsonValueKind.Array:
                    var allStrings = jsonElement.EnumerateArray().All(e => e.ValueKind == JsonValueKind.String);
                    if (allStrings)
                    {
                        return jsonElement.GetArrayLength() == 1
                            ? jsonElement[0].GetString() ?? string.Empty
                            : jsonElement.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray();
                    }
                    // return raw JSON for arrays of objects
                    return jsonElement.ToString();
                case JsonValueKind.Number:
                    return jsonElement.GetDecimal().ToString();
                case JsonValueKind.True:
                    return "true";
                case JsonValueKind.False:
                    return "false";
                case JsonValueKind.Object:
                    return jsonElement.ToString();
                default:
                    return value.ToString() ?? string.Empty;
            }
        }

        if (value is string[] stringArray && stringArray.Length == 1)
        {
            return stringArray[0];
        }
        
        return value.ToString() ?? string.Empty;
    }

    public void ClearAccumulatedFormData(ISession session)
    {
        session.Remove(SessionKeyFormData);
        logger.LogInformation("Cleared accumulated form data from session");
    }

    /// <summary>
    /// Filters out infected files from form data using Redis blacklist.
    /// Uses direct key lookup instead of KEYS command for better reliability and performance.
    /// Checks both file ID-based and filename-based blacklists.
    /// </summary>
    private Dictionary<string, object> FilterInfectedFilesFromData(Dictionary<string, object> data, string? applicationId)
    {
        try
        {
            var db = redis.GetDatabase();
            int filteredCount = 0;
            
            // Filter infected files from each field
            var filteredData = new Dictionary<string, object>();
            foreach (var kvp in data)
            {
                var fieldValue = kvp.Value?.ToString();
                if (string.IsNullOrEmpty(fieldValue))
                {
                    filteredData[kvp.Key] = kvp.Value;
                    continue;
                }
                
                // Try to parse as file list
                try
                {
                    var files = JsonSerializer.Deserialize<List<JsonElement>>(fieldValue);
                    if (files != null)
                    {
                        // Filter out infected files by checking each file against BOTH blacklist types
                        var cleanFiles = new List<JsonElement>();
                        foreach (var file in files)
                        {
                            bool isInfected = false;
                            string? originalFileName = null;
                            
                            // Get file properties
                            if (file.TryGetProperty("id", out var idProp) && 
                                Guid.TryParse(idProp.GetString(), out var fileId))
                            {
                                // Check by file ID
                                var fileIdBlacklistKey = $"{FlexFormsCacheKeys.InfectedFilePrefix}{fileId}";
                                if (db.KeyExists(fileIdBlacklistKey))
                                {
                                    isInfected = true;
                                }
                                
                                // Also check by filename if applicationId is available
                                if (!isInfected && !string.IsNullOrEmpty(applicationId))
                                {
                                    if (file.TryGetProperty("originalFileName", out var fileNameProp))
                                    {
                                        originalFileName = fileNameProp.GetString();
                                    }
                                    
                                    if (!string.IsNullOrEmpty(originalFileName))
                                    {
                                        var filenameBlacklistKey = $"{FlexFormsCacheKeys.InfectedFileNamePrefix}{applicationId}:{originalFileName}";
                                        if (db.KeyExists(filenameBlacklistKey))
                                        {
                                            isInfected = true;
                                        }
                                    }
                                }
                                
                                if (!isInfected)
                                {
                                    cleanFiles.Add(file);
                                }
                                else
                                {
                                    filteredCount++;
                                    logger.LogWarning(
                                        "Filtered infected file {FileId} ({FileName}) from field {FieldKey}",
                                        fileId,
                                        originalFileName ?? "unknown",
                                        kvp.Key);
                                }
                            }
                            else
                            {
                                // Keep files without valid ID (might not be file uploads)
                                cleanFiles.Add(file);
                            }
                        }
                        
                        // Update with cleaned list
                        filteredData[kvp.Key] = JsonSerializer.Serialize(cleanFiles);
                        continue;
                    }
                }
                catch
                {
                    // Not a file list, keep as-is
                }
                
                // Not a file list, keep original value
                filteredData[kvp.Key] = kvp.Value;
            }
            
            if (filteredCount > 0)
            {
                logger.LogInformation(
                    "Filtered {Count} infected file(s) from form data",
                    filteredCount);
            }
            
            return filteredData;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error filtering infected files from data");
            return data; // Return original data if filtering fails
        }
    }

    public string TransformToResponseJson(
        Dictionary<string, object> formData,
        Dictionary<string, string> taskStatusData,
        FormTemplate? template = null)
    {
        var responseData = new Dictionary<string, object>();
        var fieldLookup = ResponseFieldMetadataResolver.BuildLookup(template);

        // Add form field data
        foreach (var kvp in formData)
        {
            var fieldId = kvp.Key;
            var value = kvp.Value?.ToString() ?? string.Empty;

            responseData[fieldId] = ResponseFieldMetadataResolver.BuildFormFieldEntry(fieldId, value, fieldLookup);
        }

        // Add task completion status (unchanged shape)
        foreach (var kvp in taskStatusData)
        {
            var taskStatusKey = $"TaskStatus_{kvp.Key}";
            responseData[taskStatusKey] = new
            {
                value = kvp.Value,
                completed = true
            };
        }

        return JsonSerializer.Serialize(responseData, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });
    }

    private async Task<FormTemplate?> TryGetTemplateFromSessionAsync(ISession session, CancellationToken cancellationToken)
    {
        try
        {
            var templateId = session.GetString("TemplateId");
            if (string.IsNullOrWhiteSpace(templateId))
            {
                logger.LogWarning("No TemplateId in session when saving application response; question/dataType will use runtime fallbacks only");
                return null;
            }

            return await formTemplateProvider.GetTemplateAsync(templateId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load template for response enrichment; continuing with runtime fallbacks");
            return null;
        }
    }

    public Dictionary<string, string> GetTaskStatusFromSession(Guid applicationId, ISession session)
    {
        var taskStatusData = new Dictionary<string, string>();
        
        var sessionKeys = session.Keys.Where(k => k.StartsWith($"TaskStatus_{applicationId}_")).ToList();
        
        foreach (var sessionKey in sessionKeys)
        {
            var taskId = sessionKey.Substring($"TaskStatus_{applicationId}_".Length);
            var statusValue = session.GetString(sessionKey);
            
            if (!string.IsNullOrEmpty(statusValue))
            {
                taskStatusData[taskId] = statusValue;
            }
        }
        
        return taskStatusData;
    }

    public void SaveTaskStatusToSession(Guid applicationId, string taskId, string status, ISession session)
    {
        var sessionKey = $"TaskStatus_{applicationId}_{taskId}";
        session.SetString(sessionKey, status);
    }

    public void StoreFormDataInSession(Dictionary<string, object> formData, ISession session)
    {
        // Clear existing data and store new data
        ClearAccumulatedFormData(session);
        AccumulateFormData(formData, session);
    }

    public void SetCurrentAccumulatedApplicationId(Guid applicationId, ISession session)
    {
        session.SetString("CurrentAccumulatedApplicationId", applicationId.ToString());
    }

} 