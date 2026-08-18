using System.Text.Json;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Caching;
using GovUK.Dfe.FlexForms.Domain.FormEngine;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.Extensions.Logging;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

/// <summary>
/// Prepares form-engine GET state after common HTTP initialization.
/// </summary>
public interface IPrepareFormEngineGet
{
    Task<FormEngineOutcome> ExecuteAsync(
        FormEngineWorkState state,
        bool isPreview,
        bool isBackNav,
        bool isEditable,
        CancellationToken cancellationToken = default);
}

public sealed class PrepareFormEngineGetService(
    ITemplateManagementService templateManagementService,
    IApplicationResponseService applicationResponseService,
    ICollectionFlowProgressStore collectionFlowProgressStore,
    IFormSessionStore sessionStore,
    IConditionalLogicOrchestrator conditionalLogicOrchestrator,
    IFormStateManager formStateManager,
    IFormFileFieldService formFileFieldService,
    IComplexFieldConfigurationService complexFieldConfigurationService,
    IDerivedCollectionFlowService derivedCollectionFlowService,
    IApplicationsClient applicationsClient,
    INavigationHistoryService navigationHistoryService,
    IApplicationStateService applicationStateService,
    ILogger<PrepareFormEngineGetService> logger) : IPrepareFormEngineGet
{
    public async Task<FormEngineOutcome> ExecuteAsync(
        FormEngineWorkState state,
        bool isPreview,
        bool isBackNav,
        bool isEditable,
        CancellationToken cancellationToken = default)
    {
        if (state.Template == null)
        {
            logger.LogError(
                "Template is null after CommonFormEngineInitializationAsync for ReferenceNumber: {ReferenceNumber}",
                state.ReferenceNumber);
            state.Template = FormEngineConstants.CreateDummyTemplate();
        }

        if (isPreview)
        {
            state.CurrentFormState = FormState.ApplicationPreview;
            state.CurrentGroup = null;
            state.CurrentTask = null;
            state.CurrentPage = null;
            await RefreshFileValidationGateAsync(state, cancellationToken);
        }
        else
        {
            if (!isEditable && !string.IsNullOrEmpty(state.CurrentPageId))
                return FormEngineOutcome.Redirect($"~/applications/{state.ReferenceNumber}");

            ResolveRoute(state);
        }

        CheckAndClearSessionForNewApplication(state);
        await LoadAccumulatedDataFromSessionAsync(state);
        MergeFlowProgressIntoFormDataForSummary(state);

        if (!string.IsNullOrEmpty(state.DerivedFlowId) && !string.IsNullOrEmpty(state.DerivedItemId) && state.CurrentTask != null)
        {
            var derivedConfig = FormStepPolicy.GetDerivedFlow(state.CurrentTask, state.DerivedFlowId);
            if (derivedConfig != null)
                FormEngineDerivedItems.LoadItemData(derivedConfig, state.DerivedItemId, state, derivedCollectionFlowService, logger);
        }

        PopulateUploadFieldsFromSession(state);
        await OverlayFileValidationFromDatabaseAsync(state, cancellationToken);

        state.ConditionalState = await FormEngineConditionalLogic.ApplyAsync(
            state.Template,
            state.Data,
            state.FormData,
            conditionalLogicOrchestrator,
            state.CurrentPageId,
            state.TaskId,
            "load",
            logger);

        if (state.CurrentTask != null)
        {
            var isSummary = state.CurrentFormState == FormState.TaskSummary
                || formStateManager.ShouldShowDerivedCollectionFlowSummary(state.CurrentTask);
            if (isSummary)
            {
                var taskStatus = applicationStateService.CalculateTaskStatus(
                    state.CurrentTask.TaskId,
                    state.Template,
                    state.FormData,
                    state.ApplicationId,
                    state.ApplicationStatus);
                state.IsTaskCompleted = taskStatus == Domain.Models.TaskStatus.Completed;
            }
        }

        if (isBackNav)
        {
            try
            {
                var scope = FormRouteParser.HistoryScope(state.ReferenceNumber, state.TaskId, state.CurrentPageId);
                navigationHistoryService.Pop(scope);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to pop navigation history for back navigation");
            }
        }

        return FormEngineOutcome.Stay(
            formState: state.CurrentFormState,
            clearModelState: true,
            isTaskCompleted: state.IsTaskCompleted,
            conditionalState: state.ConditionalState,
            fileValidationBlocksSubmit: _fileValidationBlocksSubmit,
            blockingFiles: _blockingFiles);
    }

    private bool _fileValidationBlocksSubmit;
    private IReadOnlyList<FileValidationBlockDto> _blockingFiles = [];

    private void ResolveRoute(FormEngineWorkState state)
    {
        if (!string.IsNullOrEmpty(state.CurrentPageId) && state.Template != null)
        {
            if (FormRouteParser.TryParseCollectionFlow(state.CurrentPageId, out var flowRoute))
            {
                state.FlowId = flowRoute.FlowId;
                state.InstanceId = flowRoute.InstanceId;
                state.FlowPageId = flowRoute.PageId;

                var (group, task) = templateManagementService.FindTask(state.Template, state.TaskId);
                state.CurrentGroup = group;
                state.CurrentTask = task;

                var flowPages = FormStepPolicy.GetCollectionFlowPages(task, flowRoute.FlowId);
                var flowFieldId = FormStepPolicy.GetCollectionFlowFieldId(task, flowRoute.FlowId);

                if (!string.IsNullOrEmpty(flowFieldId))
                {
                    var existenceKey = FormSessionKeys.FlowItemExisted(flowRoute.FlowId, flowRoute.InstanceId);
                    if (sessionStore.GetString(existenceKey) == null)
                    {
                        var existed = IsExistingCollectionItem(flowFieldId, flowRoute.InstanceId);
                        sessionStore.SetString(existenceKey, existed ? "true" : "false");
                    }
                }

                if (flowPages != null)
                {
                    var page = FormStepPolicy.ResolvePage(flowPages, flowRoute.PageId);
                    if (page != null)
                    {
                        state.CurrentPage = page;
                        state.CurrentFormState = FormState.FormPage;
                        LoadExistingFlowItemData(state, flowRoute.FlowId, flowRoute.InstanceId);

                        var progressData = collectionFlowProgressStore.Load(flowRoute.FlowId, flowRoute.InstanceId);
                        foreach (var kvp in progressData)
                            state.Data[kvp.Key] = kvp.Value;
                    }
                }

                return;
            }

            if (FormRouteParser.TryParseDerivedFlow(state.CurrentPageId, out var derivedRoute))
            {
                var (group, task) = templateManagementService.FindTask(state.Template, state.TaskId);
                state.CurrentGroup = group;
                state.CurrentTask = task;
                state.DerivedFlowId = derivedRoute.FlowId;
                state.DerivedItemId = derivedRoute.ItemId;
                state.DerivedPageId = derivedRoute.PageId;

                var derivedConfig = FormStepPolicy.GetDerivedFlow(task, derivedRoute.FlowId);
                if (derivedConfig != null)
                {
                    var page = FormStepPolicy.ResolvePage(derivedConfig.Pages, derivedRoute.PageId);
                    if (page != null)
                    {
                        state.CurrentPage = page;
                        state.CurrentFormState = FormState.FormPage;
                        FormEngineDerivedItems.LoadItemData(
                            derivedConfig,
                            derivedRoute.ItemId,
                            state,
                            derivedCollectionFlowService,
                            logger);
                        var displayName = FormEngineDerivedItems.GetDisplayName(
                            derivedConfig,
                            derivedRoute.ItemId,
                            state.FormData,
                            derivedCollectionFlowService);
                        FormEngineDerivedItems.ApplyDisplayNamePlaceholders(state.CurrentPage, displayName);
                    }
                }

                return;
            }

            var found = templateManagementService.FindPage(state.Template, state.CurrentPageId);
            state.CurrentGroup = found.Group;
            state.CurrentTask = found.Task;
            state.CurrentPage = found.Page;
            return;
        }

        if (!string.IsNullOrEmpty(state.TaskId) && state.Template != null)
        {
            var (group, task) = templateManagementService.FindTask(state.Template, state.TaskId);
            state.CurrentGroup = group;
            state.CurrentTask = task;
            state.CurrentPage = null;

            if (formStateManager.ShouldShowCollectionFlowSummary(state.CurrentTask))
                state.CurrentFormState = FormState.TaskSummary;
            else if (formStateManager.ShouldShowDerivedCollectionFlowSummary(state.CurrentTask))
                state.CurrentFormState = FormState.DerivedCollectionFlowSummary;
        }
    }

    private void CheckAndClearSessionForNewApplication(FormEngineWorkState state)
    {
        var sessionApplicationId = sessionStore.GetString(FormSessionKeys.CurrentAccumulatedApplicationId);
        var currentApplicationId = state.ApplicationId?.ToString();

        if (!string.IsNullOrEmpty(sessionApplicationId) && sessionApplicationId != currentApplicationId)
        {
            applicationResponseService.ClearAccumulatedFormData();
            logger.LogInformation(
                "Cleared accumulated form data for previous application {PreviousApplicationId}, now working with {CurrentApplicationId}",
                sessionApplicationId,
                currentApplicationId);
        }

        if (state.ApplicationId.HasValue)
        {
            sessionStore.SetString(
                FormEngineConstants.CurrentAccumulatedApplicationIdWriteKey,
                state.ApplicationId.Value.ToString());
        }
    }

    private async Task LoadAccumulatedDataFromSessionAsync(FormEngineWorkState state)
    {
        var accumulatedData = applicationResponseService.GetAccumulatedFormData();
        if (accumulatedData.Count > 0)
        {
            foreach (var kvp in accumulatedData)
                state.Data[kvp.Key] = kvp.Value;

            logger.LogInformation("Loaded {Count} accumulated form data entries from session", accumulatedData.Count);
        }

        state.ConditionalState = await FormEngineConditionalLogic.ApplyAsync(
            state.Template,
            state.Data,
            state.FormData,
            conditionalLogicOrchestrator,
            state.CurrentPageId,
            state.TaskId,
            "load",
            logger);
    }

    private void MergeFlowProgressIntoFormDataForSummary(FormEngineWorkState state)
    {
        if (state.CurrentTask?.Summary?.Mode?.Equals("multiCollectionFlow", StringComparison.OrdinalIgnoreCase) != true
            || state.CurrentTask.Summary?.Flows == null)
        {
            return;
        }

        foreach (var flow in state.CurrentTask.Summary.Flows)
        {
            if (!state.FormData.TryGetValue(flow.FieldId, out var val) || string.IsNullOrWhiteSpace(val?.ToString()))
                continue;

            var items = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(val.ToString()!) ?? [];
            var changed = false;

            foreach (var item in items)
            {
                if (!item.TryGetValue("id", out var idObj))
                    continue;
                var instanceId = idObj?.ToString();
                if (string.IsNullOrWhiteSpace(instanceId))
                    continue;

                var progress = collectionFlowProgressStore.Load(flow.FlowId, instanceId);
                if (progress.Count == 0)
                    continue;

                foreach (var kv in progress)
                    item[kv.Key] = kv.Value;
                changed = true;
            }

            if (!changed)
                continue;

            var updatedJson = JsonSerializer.Serialize(items);
            state.FormData[flow.FieldId] = updatedJson;
            state.Data[flow.FieldId] = updatedJson;
        }
    }

    private void PopulateUploadFieldsFromSession(FormEngineWorkState state)
    {
        if (state.CurrentPage == null || !state.ApplicationId.HasValue)
            return;

        var uploadFields = state.CurrentPage.Fields
            .Where(f => f.Type == "complexField"
                && f.ComplexField != null
                && complexFieldConfigurationService.GetConfiguration(f.ComplexField.Id).FieldType
                    .Equals("upload", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var field in uploadFields)
        {
            var fieldId = field.FieldId;
            var files = formFileFieldService.GetFiles(state.FileFieldContext, fieldId);
            formFileFieldService.SaveFiles(state.FileFieldContext, fieldId, files.ToList());
            if (files.Count == 0)
                continue;

            state.Data[fieldId] = JsonSerializer.Serialize(files);
        }
    }

    private async Task OverlayFileValidationFromDatabaseAsync(
        FormEngineWorkState state,
        CancellationToken cancellationToken)
    {
        if (!state.ApplicationId.HasValue)
            return;

        IReadOnlyList<UploadDto>? dbFiles;
        try
        {
            dbFiles = await applicationsClient.GetFilesForApplicationAsync(state.ApplicationId.Value, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Could not refresh file validation status from API for application {ApplicationId}",
                state.ApplicationId);
            return;
        }

        if (dbFiles is null || dbFiles.Count == 0)
            return;

        var latestById = FileValidationStatusOverlay.IndexById(dbFiles);
        if (latestById.Count == 0)
            return;

        FileValidationStatusOverlay.ApplyToFormData(state.FormData, latestById);
        FileValidationStatusOverlay.ApplyToFormData(state.Data, latestById);
        applicationResponseService.StoreFormDataInSession(state.FormData);

        if (state.CurrentPage == null)
            return;

        var uploadFields = state.CurrentPage.Fields
            .Where(f => f.Type == "complexField"
                && f.ComplexField != null
                && complexFieldConfigurationService.GetConfiguration(f.ComplexField.Id).FieldType
                    .Equals("upload", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var field in uploadFields)
        {
            var files = formFileFieldService.GetFiles(state.FileFieldContext, field.FieldId).ToList();
            FileValidationStatusOverlay.ApplyToFiles(files, latestById);
            formFileFieldService.SaveFiles(state.FileFieldContext, field.FieldId, files);
            if (files.Count > 0)
                state.Data[field.FieldId] = JsonSerializer.Serialize(files);
        }
    }

    private void LoadExistingFlowItemData(FormEngineWorkState state, string flowId, string instanceId)
    {
        var fieldId = FormStepPolicy.GetCollectionFlowFieldId(state.CurrentTask, flowId);
        if (string.IsNullOrEmpty(fieldId))
            return;

        var accumulated = applicationResponseService.GetAccumulatedFormData();
        if (accumulated.TryGetValue(fieldId, out var collectionValue))
        {
            var json = collectionValue?.ToString() ?? "[]";
            try
            {
                var items = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json) ?? [];
                var existingItem = items.FirstOrDefault(item => item.TryGetValue("id", out var id) && id?.ToString() == instanceId);

                if (existingItem != null)
                {
                    foreach (var kvp in existingItem)
                    {
                        if (kvp.Key == "id")
                            continue;
                        state.Data[kvp.Key] = kvp.Value;
                    }
                }
                else
                {
                    LoadOrClearProgress(state, flowId, instanceId);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load existing flow item data for instance {InstanceId}", instanceId);
            }
        }
        else
        {
            LoadOrClearProgress(state, flowId, instanceId);
        }
    }

    private void LoadOrClearProgress(FormEngineWorkState state, string flowId, string instanceId)
    {
        var existingProgress = collectionFlowProgressStore.Load(flowId, instanceId);
        if (existingProgress.Count > 0)
        {
            foreach (var kvp in existingProgress)
                state.Data[kvp.Key] = kvp.Value;
            return;
        }

        collectionFlowProgressStore.Clear(flowId, instanceId);
        state.Data.Clear();
    }

    private bool IsExistingCollectionItem(string fieldId, string instanceId)
    {
        var accumulated = applicationResponseService.GetAccumulatedFormData();
        return FormEngineCollectionItems.Read(accumulated, fieldId)
            .Any(item => item.TryGetValue("id", out var id) && id?.ToString() == instanceId);
    }

    private async Task RefreshFileValidationGateAsync(FormEngineWorkState state, CancellationToken cancellationToken)
    {
        _fileValidationBlocksSubmit = false;
        _blockingFiles = [];

        if (!state.ApplicationId.HasValue)
            return;

        try
        {
            var gate = await applicationsClient.GetFileValidationGateAsync(state.ApplicationId.Value);
            _fileValidationBlocksSubmit = !gate.CanSubmit;
            _blockingFiles = gate.BlockingFiles ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not evaluate file validation gate for application {ApplicationId}", state.ApplicationId);
        }
    }
}
