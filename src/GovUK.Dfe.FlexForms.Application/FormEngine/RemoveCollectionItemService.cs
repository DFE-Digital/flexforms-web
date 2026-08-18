using System.Text.Json;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Validation;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

/// <summary>
/// Removes an item from a collection-flow field and deletes associated files.
/// </summary>
public interface IRemoveCollectionItem
{
    Task<FormEngineOutcome> ExecuteAsync(
        FormEngineWorkState state,
        string fieldId,
        string itemId,
        string? flowId,
        bool confirmed,
        CancellationToken cancellationToken = default);
}

public sealed class RemoveCollectionItemService(
    ITemplateManagementService templateManagementService,
    IApplicationResponseService applicationResponseService,
    IFileUploadService fileUploadService,
    IFormNavigationService formNavigationService,
    ILogger<RemoveCollectionItemService> logger) : IRemoveCollectionItem
{
    public async Task<FormEngineOutcome> ExecuteAsync(
        FormEngineWorkState state,
        string fieldId,
        string itemId,
        string? flowId,
        bool confirmed,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(state.TaskId) && state.Template != null)
        {
            var (group, task) = templateManagementService.FindTask(state.Template, state.TaskId);
            state.CurrentGroup = group;
            state.CurrentTask = task;
        }

        if (string.IsNullOrEmpty(fieldId) || string.IsNullOrEmpty(itemId))
            return FormEngineOutcome.BadRequest(FormEngineMessages.FieldIdAndItemIdRequired);

        if (!state.IsEditable)
        {
            return FormEngineOutcome.Stay(
                clearModelState: true,
                errors: [new FormValidationError(string.Empty, FormEngineMessages.NoWritePermission)]);
        }

        var summaryUrl = formNavigationService.GetCollectionFlowSummaryUrl(state.TaskId, state.ReferenceNumber);
        if (!confirmed)
        {
            logger.LogInformation("RemoveCollectionItem handler executing for validation - item will not be removed yet");
            return FormEngineOutcome.Redirect(summaryUrl);
        }

        logger.LogInformation(
            "RemoveCollectionItem handler executing confirmed removal for item {ItemId} from field {FieldId}",
            itemId,
            fieldId);

        var accumulatedData = applicationResponseService.GetAccumulatedFormData();
        string? successMessage = null;

        if (!string.IsNullOrEmpty(flowId) && state.CurrentTask != null)
        {
            var flow = state.CurrentTask.Summary?.Flows?.FirstOrDefault(f => f.FlowId == flowId);
            if (flow != null)
            {
                Dictionary<string, object>? itemData = null;
                if (accumulatedData.TryGetValue(fieldId, out var collectionValue))
                {
                    var json = collectionValue?.ToString() ?? "[]";
                    try
                    {
                        var items = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json) ?? [];
                        itemData = items.FirstOrDefault(i => i.TryGetValue("id", out var id) && id?.ToString() == itemId);
                    }
                    catch (JsonException)
                    {
                        itemData = null;
                    }
                }

                itemData = FormEngineSuccessMessages.ExpandEncodedJson(itemData);
                successMessage = FormEngineSuccessMessages.Generate(flow.DeleteItemMessage, "delete", itemData, flow.Title);
            }
        }

        if (accumulatedData.TryGetValue(fieldId, out var collectionData))
        {
            var json = collectionData?.ToString() ?? "[]";
            try
            {
                var items = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json) ?? [];
                var itemToRemove = items.FirstOrDefault(item =>
                    item.TryGetValue("id", out var id) && id?.ToString() == itemId);

                if (itemToRemove != null && state.ApplicationId.HasValue)
                {
                    var expandedItem = FormEngineSuccessMessages.ExpandEncodedJson(itemToRemove);
                    await DeleteFilesFromCollectionItemAsync(state.ApplicationId.Value, expandedItem, cancellationToken);
                }

                items.RemoveAll(item => item.TryGetValue("id", out var id) && id?.ToString() == itemId);
                var updatedJson = JsonSerializer.Serialize(items);
                applicationResponseService.AccumulateFormData(new Dictionary<string, object> { [fieldId] = updatedJson });

                if (state.ApplicationId.HasValue)
                {
                    await applicationResponseService.SaveApplicationResponseAsync(
                        state.ApplicationId.Value,
                        new Dictionary<string, object> { [fieldId] = updatedJson },
                        cancellationToken);
                }
            }
            catch (ExternalApplicationsException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to remove collection item {ItemId} from field {FieldId}", itemId, fieldId);
            }
        }

        return FormEngineOutcome.Redirect(summaryUrl, successMessage);
    }

    private async Task DeleteFilesFromCollectionItemAsync(
        Guid applicationId,
        Dictionary<string, object>? itemData,
        CancellationToken cancellationToken)
    {
        if (itemData == null)
            return;

        var deletedCount = 0;
        foreach (var kvp in itemData)
        {
            if (kvp.Key == "id" || kvp.Value == null)
                continue;

            try
            {
                var valueStr = kvp.Value.ToString();
                if (string.IsNullOrEmpty(valueStr) || !valueStr.TrimStart().StartsWith('['))
                    continue;

                var files = JsonSerializer.Deserialize<List<UploadDto>>(valueStr);
                if (files == null || files.Count == 0)
                    continue;

                foreach (var file in files)
                {
                    try
                    {
                        await fileUploadService.DeleteFileAsync(file.Id, applicationId, cancellationToken);
                        deletedCount++;
                        logger.LogInformation(
                            "Deleted file {FileId} ({FileName}) from removed collection item in application {ApplicationId}",
                            file.Id,
                            file.OriginalFileName,
                            applicationId);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(
                            ex,
                            "Failed to delete file {FileId} from collection item - file may already be deleted",
                            file.Id);
                    }
                }
            }
            catch (JsonException)
            {
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error processing field {FieldKey} for file cleanup", kvp.Key);
            }
        }

        if (deletedCount > 0)
        {
            logger.LogInformation(
                "Successfully deleted {DeletedCount} file(s) from removed collection item in application {ApplicationId}",
                deletedCount,
                applicationId);
        }
    }
}
