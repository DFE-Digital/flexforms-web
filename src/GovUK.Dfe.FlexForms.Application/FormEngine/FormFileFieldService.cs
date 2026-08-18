using System.Text.Json;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Caching;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

public sealed class FormFileFieldService(
    IFormSessionStore sessionStore,
    ICollectionFlowProgressStore progressStore,
    IInfectedUploadFilter infectedUploadFilter,
    IInfectedFileStore infectedFileStore,
    IApplicationResponseService applicationResponseService,
    ILogger<FormFileFieldService> logger) : IFormFileFieldService
{
    public IReadOnlyList<UploadDto> GetFiles(FormFileFieldContext context, string fieldId)
    {
        if (string.IsNullOrEmpty(fieldId))
            return Array.Empty<UploadDto>();

        var applicationId = context.ApplicationId?.ToString();

        if (context.IsCollectionFlow)
        {
            var progressData = progressStore.Load(context.FlowId!, context.InstanceId!);
            if (progressData.TryGetValue(fieldId, out var progressValue)
                && TryParseFiles(progressValue, out var sessionFiles))
            {
                return infectedUploadFilter.FilterList(sessionFiles, applicationId);
            }

            return GetFilesFromAccumulatedCollection(context, fieldId, applicationId);
        }

        if (context.ApplicationId is { } appId)
        {
            var sessionFilesJson = sessionStore.GetString(FormSessionKeys.UploadedFiles(appId, fieldId));
            if (TryParseFiles(sessionFilesJson, out var sessionFiles))
                return infectedUploadFilter.FilterList(sessionFiles, applicationId);
        }

        var accumulatedData = applicationResponseService.GetAccumulatedFormData();
        if (accumulatedData.TryGetValue(fieldId, out var fieldValue)
            && TryParseFiles(fieldValue, out var accumulatedFiles))
        {
            return infectedUploadFilter.FilterList(accumulatedFiles, applicationId);
        }

        return Array.Empty<UploadDto>();
    }

    public void SaveFiles(FormFileFieldContext context, string fieldId, IReadOnlyList<UploadDto> files)
    {
        if (string.IsNullOrEmpty(fieldId))
            return;

        var serialized = JsonSerializer.Serialize(files);

        if (context.IsCollectionFlow)
        {
            progressStore.SetField(context.FlowId!, context.InstanceId!, fieldId, serialized);
            return;
        }

        if (context.ApplicationId is not { } appId)
            return;

        sessionStore.SetString(FormSessionKeys.UploadedFiles(appId, fieldId), serialized);
    }

    public void ReplaceUploadPlaceholders(Dictionary<string, object> data, FormFileFieldContext context)
    {
        var applicationId = context.ApplicationId?.ToString();

        if (context.IsCollectionFlow)
        {
            var flowProgress = progressStore.Load(context.FlowId!, context.InstanceId!);
            var accumulatedData = applicationResponseService.GetAccumulatedFormData();

            foreach (var key in data.Keys.ToList())
            {
                if (data[key]?.ToString() != FormEngineConstants.UploadFieldSessionPlaceholder)
                    continue;

                if (flowProgress.TryGetValue(key, out var sessionValue))
                {
                    data[key] = infectedUploadFilter.FilterUploadDataJson(sessionValue?.ToString(), applicationId);
                    logger.LogInformation(
                        "Collection flow: Replaced upload placeholder for field {FieldId} with filtered session data",
                        key);
                    continue;
                }

                logger.LogWarning("Collection flow: Session empty for field {FieldId}, falling back to database", key);

                try
                {
                    foreach (var kvp in accumulatedData)
                    {
                        var collectionJson = kvp.Value?.ToString();
                        if (string.IsNullOrWhiteSpace(collectionJson))
                            continue;

                        var items = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(collectionJson);
                        if (items == null)
                            continue;

                        var existingItem = items.FirstOrDefault(item =>
                            item.TryGetValue("id", out var idVal) && idVal?.ToString() == context.InstanceId);
                        if (existingItem == null || !existingItem.TryGetValue(key, out var fieldValue))
                            continue;

                        var fieldValueStr = fieldValue?.ToString();
                        if (string.IsNullOrWhiteSpace(fieldValueStr))
                            continue;

                        data[key] = infectedUploadFilter.FilterUploadDataJson(fieldValueStr, applicationId);
                        logger.LogInformation(
                            "Collection flow: Replaced upload placeholder for field {FieldId} with filtered database data",
                            key);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Collection flow: Error getting database data for field {FieldId}", key);
                }
            }

            return;
        }

        foreach (var key in data.Keys.ToList())
        {
            if (data[key]?.ToString() != FormEngineConstants.UploadFieldSessionPlaceholder)
                continue;

            if (context.ApplicationId is not { } appId)
                continue;

            var sessionFilesJson = sessionStore.GetString(FormSessionKeys.UploadedFiles(appId, key));
            if (!string.IsNullOrWhiteSpace(sessionFilesJson))
            {
                data[key] = infectedUploadFilter.FilterUploadDataJson(sessionFilesJson, applicationId);
                logger.LogInformation(
                    "Replaced upload placeholder for field {FieldId} with filtered session data from upload key",
                    key);
            }
            else
            {
                logger.LogInformation(
                    "No session data found for upload field {FieldId} - validation will detect empty field",
                    key);
            }
        }
    }

    public bool ContainsFileName(FormFileFieldContext context, string fieldId, string fileName)
    {
        if (context.ApplicationId is { } appId)
        {
            try
            {
                if (infectedFileStore.IsFileNameInfected(appId.ToString(), fileName))
                    return false;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error checking infected blacklist for file '{FileName}'", fileName);
            }
        }

        var files = GetFiles(context, fieldId);
        if (files.Count > 0)
            return files.Any(f => string.Equals(f.OriginalFileName, fileName, StringComparison.OrdinalIgnoreCase));

        string? rawJson = null;
        if (context.IsCollectionFlow)
        {
            var progress = progressStore.Load(context.FlowId!, context.InstanceId!);
            if (progress.TryGetValue(fieldId, out var filesJson))
                rawJson = filesJson?.ToString();
        }
        else if (context.ApplicationId is { } regularAppId)
        {
            rawJson = sessionStore.GetString(FormSessionKeys.UploadedFiles(regularAppId, fieldId));
        }

        return !string.IsNullOrEmpty(rawJson)
               && rawJson.IndexOf(fileName, StringComparison.InvariantCultureIgnoreCase) >= 0;
    }

    private IReadOnlyList<UploadDto> GetFilesFromAccumulatedCollection(
        FormFileFieldContext context,
        string fieldId,
        string? applicationId)
    {
        try
        {
            var accumulatedData = applicationResponseService.GetAccumulatedFormData();
            foreach (var kvp in accumulatedData)
            {
                var collectionJson = kvp.Value?.ToString();
                if (string.IsNullOrWhiteSpace(collectionJson))
                    continue;

                try
                {
                    var items = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(collectionJson) ?? [];
                    var existingItem = items.FirstOrDefault(item =>
                        item.TryGetValue("id", out var idVal) && idVal?.ToString() == context.InstanceId);
                    if (existingItem == null
                        || !existingItem.TryGetValue(fieldId, out var innerValue)
                        || innerValue == null)
                        continue;

                    if (TryParseFiles(innerValue, out var files))
                        return infectedUploadFilter.FilterList(files, applicationId);
                }
                catch (Exception)
                {
                    // Ignore parse errors for non-collection fields
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing accumulated data for collection flow");
        }

        return Array.Empty<UploadDto>();
    }

    private static bool TryParseFiles(object? value, out List<UploadDto> files)
    {
        files = [];
        if (value == null)
            return false;

        if (value is List<UploadDto> list)
        {
            files = list;
            return true;
        }

        if (value is JsonElement innerElem)
        {
            if (innerElem.ValueKind == JsonValueKind.Array)
            {
                try
                {
                    files = JsonSerializer.Deserialize<List<UploadDto>>(innerElem.GetRawText()) ?? [];
                    return true;
                }
                catch (JsonException)
                {
                    return false;
                }
            }

            if (innerElem.ValueKind == JsonValueKind.String)
                return TryParseFiles(innerElem.GetString(), out files);
        }

        var json = value.ToString();
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            files = JsonSerializer.Deserialize<List<UploadDto>>(json) ?? [];
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
