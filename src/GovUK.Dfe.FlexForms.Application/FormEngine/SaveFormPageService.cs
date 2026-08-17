using System.Text.Json;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Validation;
using GovUK.Dfe.FlexForms.Domain.Caching;
using GovUK.Dfe.FlexForms.Domain.FormEngine;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.Extensions.Logging;
using PageModel = GovUK.Dfe.FlexForms.Domain.Models.Page;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

/// <summary>
/// Saves a posted form page and decides the next navigation target.
/// </summary>
public interface ISaveFormPage
{
    Task<FormEngineOutcome> ExecuteAsync(
        FormEngineWorkState state,
        IReadOnlyDictionary<string, IReadOnlyList<string>> postedFields,
        string? isTaskCompletedValue,
        CancellationToken cancellationToken = default);
}

public sealed class SaveFormPageService(
    ITemplateManagementService templateManagementService,
    IPostedFormDataBinder postedFormDataBinder,
    IFormFileFieldService formFileFieldService,
    IFormValidationOrchestrator formValidationOrchestrator,
    IApplicationResponseService applicationResponseService,
    ICollectionFlowProgressStore collectionFlowProgressStore,
    IFormSessionStore sessionStore,
    INavigationHistoryService navigationHistoryService,
    IFormNavigationService formNavigationService,
    IFormStateManager formStateManager,
    IConditionalLogicOrchestrator conditionalLogicOrchestrator,
    IComplexFieldConfigurationService complexFieldConfigurationService,
    IDerivedCollectionFlowService derivedCollectionFlowService,
    IApplicationStateService applicationStateService,
    ILogger<SaveFormPageService> logger) : ISaveFormPage
{
    public async Task<FormEngineOutcome> ExecuteAsync(
        FormEngineWorkState state,
        IReadOnlyDictionary<string, IReadOnlyList<string>> postedFields,
        string? isTaskCompletedValue,
        CancellationToken cancellationToken = default)
    {
        ResolveRoute(state);

        if (!state.IsEditable)
        {
            return FormEngineOutcome.Stay(
                errors: [new FormValidationError(string.Empty, FormEngineMessages.NoWritePermission)]);
        }

        state.Data = postedFormDataBinder.Bind(postedFields, state.Data);
        formFileFieldService.ReplaceUploadPlaceholders(state.Data, state.FileFieldContext);

        state.ConditionalState = await FormEngineConditionalLogic.ApplyAsync(
            state.Template,
            state.Data,
            state.FormData,
            conditionalLogicOrchestrator,
            state.CurrentPageId,
            state.TaskId,
            "change",
            logger,
            applicationResponseService.GetAccumulatedFormData());

        postedFormDataBinder.ApplyDateParts(postedFields, state.Data);

        var isDerivedFlowRoute = FormRouteParser.TryParseDerivedFlow(state.CurrentPageId, out _);
        if (!isDerivedFlowRoute && state.CurrentPage != null)
        {
            var validation = formValidationOrchestrator.ValidatePage(state.CurrentPage, state.Data, state.Template);
            if (!validation.IsValid)
                return await InvalidPageOutcomeAsync(state, validation);
        }

        MergeAutocompleteMultiSelect(state);

        var isSubFlow = FormRouteParser.TryParseCollectionFlow(state.CurrentPageId, out _);
        var isDerivedFlowSave = FormRouteParser.TryParseDerivedFlow(state.CurrentPageId, out _);
        if (state.ApplicationId.HasValue && state.Data.Count > 0 && !isSubFlow && !isDerivedFlowSave)
        {
            await applicationResponseService.SaveApplicationResponseAsync(state.ApplicationId.Value, state.Data, cancellationToken);
            logger.LogInformation(
                "Successfully saved response for Application {ApplicationId}, Page {PageId}",
                state.ApplicationId.Value,
                state.CurrentPageId);
        }

        PushNavigationHistory(state);

        if (state.CurrentTask != null && state.CurrentPage != null)
        {
            var collectionOutcome = await TryNavigateCollectionFlowAsync(state, cancellationToken);
            if (collectionOutcome != null)
                return collectionOutcome;

            logger.LogInformation("POST: Checking if CurrentPageId '{CurrentPageId}' is a derived flow route", state.CurrentPageId);

            if (FormRouteParser.TryParseDerivedFlow(state.CurrentPageId, out var derivedRoute))
            {
                logger.LogInformation(
                    "POST: Detected derived flow route - flowId='{FlowId}', itemId='{ItemId}', pageId='{PageId}'",
                    derivedRoute.FlowId,
                    derivedRoute.ItemId,
                    derivedRoute.PageId);
            }
            else
            {
                logger.LogInformation("POST: CurrentPageId '{CurrentPageId}' is NOT a derived flow route", state.CurrentPageId);
            }

            if (FormRouteParser.TryParseDerivedFlow(state.CurrentPageId, out derivedRoute))
            {
                var derivedOutcome = await SaveDerivedFlowAsync(state, derivedRoute, cancellationToken);
                if (derivedOutcome != null)
                    return derivedOutcome;
            }
            else if (formStateManager.ShouldShowDerivedCollectionFlowSummary(state.CurrentTask))
            {
                return await CompleteDerivedSummaryFromPageAsync(state, isTaskCompletedValue, cancellationToken);
            }
            else
            {
                return await NavigateStandardPageAsync(state);
            }
        }
        else if (state.CurrentTask != null)
        {
            if (formStateManager.ShouldShowCollectionFlowSummary(state.CurrentTask))
            {
                return FormEngineOutcome.Redirect(
                    formNavigationService.GetCollectionFlowSummaryUrl(state.CurrentTask.TaskId, state.ReferenceNumber));
            }

            if (formStateManager.ShouldShowDerivedCollectionFlowSummary(state.CurrentTask))
                return await CompleteDerivedSummaryFallbackAsync(state, isTaskCompletedValue, cancellationToken);

            return FormEngineOutcome.Redirect($"/applications/{state.ReferenceNumber}/{state.CurrentTask.TaskId}");
        }

        return FormEngineOutcome.Redirect($"/applications/{state.ReferenceNumber}");
    }

    private void ResolveRoute(FormEngineWorkState state)
    {
        if (string.IsNullOrEmpty(state.CurrentPageId))
        {
            if (string.IsNullOrEmpty(state.TaskId) || state.Template == null)
                return;

            var (group, task) = templateManagementService.FindTask(state.Template, state.TaskId);
            state.CurrentGroup = group;
            state.CurrentTask = task;
            state.CurrentPage = null;
            logger.LogInformation("POST: Initialized CurrentTask '{TaskId}' for summary POST (no pageId)", state.CurrentTask?.TaskId);
            return;
        }

        if (state.Template == null)
            return;

        if (FormRouteParser.TryParseCollectionFlow(state.CurrentPageId, out var flowRoute))
        {
            var (group, task) = templateManagementService.FindTask(state.Template, state.TaskId);
            state.CurrentGroup = group;
            state.CurrentTask = task;
            var flowPages = FormStepPolicy.GetCollectionFlowPages(task, flowRoute.FlowId);
            var page = FormStepPolicy.ResolvePage(flowPages, flowRoute.PageId);
            if (page != null)
                state.CurrentPage = page;
            return;
        }

        if (FormRouteParser.TryParseDerivedFlow(state.CurrentPageId, out var derivedRoute))
        {
            var (group, task) = templateManagementService.FindTask(state.Template, state.TaskId);
            state.CurrentGroup = group;
            state.CurrentTask = task;
            var derivedConfig = FormStepPolicy.GetDerivedFlow(task, derivedRoute.FlowId);
            var page = FormStepPolicy.ResolvePage(derivedConfig?.Pages, derivedRoute.PageId);
            if (page != null)
                state.CurrentPage = page;
            return;
        }

        var found = templateManagementService.FindPage(state.Template, state.CurrentPageId);
        state.CurrentGroup = found.Group;
        state.CurrentTask = found.Task;
        state.CurrentPage = found.Page;
    }

    private async Task<FormEngineOutcome> InvalidPageOutcomeAsync(FormEngineWorkState state, FormValidationResult validation)
    {
        logger.LogWarning("ModelState invalid on POST Page");

        if (FormRouteParser.TryParseCollectionFlow(state.CurrentPageId, out var flowRoute))
        {
            try
            {
                collectionFlowProgressStore.Save(flowRoute.FlowId, flowRoute.InstanceId, state.Data);
                logger.LogInformation(
                    "Saved in-progress flow data for flow {FlowId}, instance {InstanceId} with {Count} fields due to validation errors.",
                    flowRoute.FlowId,
                    flowRoute.InstanceId,
                    state.Data.Count);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to save flow progress on validation failure.");
            }
        }

        var selfUrl = $"/applications/{state.ReferenceNumber}/{state.TaskId}/{state.CurrentPageId}";
        if (FormRouteParser.IsDerivedFlow(state.CurrentPageId) || FormRouteParser.IsCollectionFlow(state.CurrentPageId))
        {
            return FormEngineOutcome.Redirect(
                selfUrl,
                errors: validation.Errors,
                persistErrors: true,
                errorContextKey: state.ErrorContextKey);
        }

        return FormEngineOutcome.Stay(
            errors: validation.Errors,
            persistErrors: true,
            errorContextKey: state.ErrorContextKey);
    }

    private void MergeAutocompleteMultiSelect(FormEngineWorkState state)
    {
        if (state.CurrentPage == null)
            return;

        try
        {
            foreach (var field in state.CurrentPage.Fields.Where(f => f.Type == "complexField" && f.ComplexField != null))
            {
                var cfg = complexFieldConfigurationService.GetConfiguration(field.ComplexField.Id);
                if (!string.Equals(cfg.FieldType, "autocomplete", StringComparison.OrdinalIgnoreCase) || !cfg.AllowMultiple)
                    continue;

                var key = field.FieldId;
                if (!state.Data.TryGetValue(key, out var newValObj))
                    continue;

                var newVal = newValObj?.ToString();
                if (string.IsNullOrWhiteSpace(newVal))
                    continue;

                var acc = applicationResponseService.GetAccumulatedFormData();
                var list = new List<object>();
                if (acc.TryGetValue(key, out var existing) && !string.IsNullOrWhiteSpace(existing?.ToString()))
                {
                    var existingText = existing!.ToString()!;
                    var addedExisting = TryParseExistingAutocomplete(existingText, list);
                    if (!addedExisting && !string.IsNullOrWhiteSpace(existingText))
                        list.Add(existingText);
                }

                var exists = list.Any(x => (x?.ToString() ?? "") == newVal);
                if (!exists)
                    list.Add(ParseAutocompleteValue(newVal));

                var updatedJson = JsonSerializer.Serialize(list);
                state.Data[key] = updatedJson;
                state.Data[$"Data_{key}"] = updatedJson;
                applicationResponseService.AccumulateFormData(new Dictionary<string, object> { [key] = updatedJson });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to merge multi-select autocomplete values");
        }
    }

    private static bool TryParseExistingAutocomplete(string existingText, List<object> list)
    {
        try
        {
            var parsedArray = JsonSerializer.Deserialize<List<object>>(existingText);
            if (parsedArray != null)
            {
                list.AddRange(parsedArray);
                return true;
            }
        }
        catch (JsonException)
        {
        }

        try
        {
            using var doc = JsonDocument.Parse(existingText);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                list.Add(doc.RootElement.Clone());
                return true;
            }
        }
        catch (JsonException)
        {
        }

        return false;
    }

    private static object ParseAutocompleteValue(string newVal)
    {
        try
        {
            using var newDoc = JsonDocument.Parse(newVal);
            if (newDoc.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                return newDoc.RootElement.Clone();
            if (newDoc.RootElement.ValueKind == JsonValueKind.String)
                return newDoc.RootElement.GetString() ?? string.Empty;
            return newDoc.RootElement.ToString();
        }
        catch (JsonException)
        {
            return newVal;
        }
    }

    private void PushNavigationHistory(FormEngineWorkState state)
    {
        try
        {
            if (!string.IsNullOrEmpty(state.CurrentPageId))
            {
                var scope = FormRouteParser.HistoryScope(state.ReferenceNumber, state.TaskId, state.CurrentPageId);
                var currentUrl = $"/applications/{state.ReferenceNumber}/{state.TaskId}/{state.CurrentPageId}";
                navigationHistoryService.Push(scope, currentUrl);
            }
            else if (!string.IsNullOrEmpty(state.TaskId))
            {
                var scope = FormRouteParser.HistoryScope(state.ReferenceNumber, state.TaskId, state.CurrentPageId);
                var currentUrl = $"/applications/{state.ReferenceNumber}/{state.TaskId}";
                navigationHistoryService.Push(scope, currentUrl);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push navigation history");
        }
    }

    private async Task<FormEngineOutcome?> TryNavigateCollectionFlowAsync(
        FormEngineWorkState state,
        CancellationToken cancellationToken)
    {
        if (!FormRouteParser.TryParseCollectionFlow(state.CurrentPageId, out var flowRoute))
            return null;

        var flowPages = FormStepPolicy.GetCollectionFlowPages(state.CurrentTask, flowRoute.FlowId)?.ToList();
        var flowFieldId = FormStepPolicy.GetCollectionFlowFieldId(state.CurrentTask, flowRoute.FlowId);
        if (flowPages == null || string.IsNullOrEmpty(flowFieldId))
            return null;

        var existenceKey = FormSessionKeys.FlowItemExisted(flowRoute.FlowId, flowRoute.InstanceId);
        var existedValue = sessionStore.GetString(existenceKey);
        var itemExistedBeforeSave = existedValue is not null && bool.TryParse(existedValue, out var parsed)
            ? parsed
            : IsExistingCollectionItem(flowFieldId, flowRoute.InstanceId);
        _ = itemExistedBeforeSave;

        collectionFlowProgressStore.Save(flowRoute.FlowId, flowRoute.InstanceId, state.Data);

        if (state.ApplicationId.HasValue)
        {
            var accumulatedProgress = collectionFlowProgressStore.Load(flowRoute.FlowId, flowRoute.InstanceId);
            AppendCollectionItemToSession(flowPages, flowFieldId, flowRoute.InstanceId, accumulatedProgress);

            var accData = applicationResponseService.GetAccumulatedFormData();
            if (accData.TryGetValue(flowFieldId, out var collectionValue))
            {
                await applicationResponseService.SaveApplicationResponseAsync(
                    state.ApplicationId.Value,
                    new Dictionary<string, object> { [flowFieldId] = collectionValue },
                    cancellationToken);
                logger.LogInformation(
                    "Saved partial collection item to database for flow {FlowId}, instance {InstanceId}, page {PageId}",
                    flowRoute.FlowId,
                    flowRoute.InstanceId,
                    state.CurrentPageId);
            }
        }

        var index = FormStepPolicy.IndexOfPage(flowPages, state.CurrentPage!.PageId);
        var isLast = FormStepPolicy.IsLastPage(flowPages, state.CurrentPage.PageId);
        if (!isLast)
        {
            string? nextPageId = null;
            if (state.ConditionalState != null)
            {
                logger.LogDebug(
                    "Sub-flow navigation: checking conditional logic for pages. Current page: {CurrentPageId}, Flow: {FlowId}",
                    state.CurrentPage.PageId,
                    flowRoute.FlowId);

                var mergedData = collectionFlowProgressStore.Load(state.FlowId, state.InstanceId);
                foreach (var kvp in state.Data)
                    mergedData[kvp.Key] = kvp.Value;

                var navContext = new ConditionalLogicContext
                {
                    CurrentPageId = state.CurrentPageId,
                    CurrentTaskId = state.TaskId,
                    IsClientSide = false,
                    Trigger = "change"
                };

                var updatedConditionalState = await conditionalLogicOrchestrator.ApplyConditionalLogicAsync(
                    state.Template,
                    mergedData,
                    navContext);

                for (var i = index + 1; i < flowPages.Count; i++)
                {
                    var candidatePage = flowPages[i];
                    var isHidden = updatedConditionalState.PageVisibility.TryGetValue(candidatePage.PageId, out var isVisible) && !isVisible;
                    var isSkipped = updatedConditionalState.SkippedPages.Contains(candidatePage.PageId);
                    if (!isHidden && !isSkipped)
                    {
                        nextPageId = candidatePage.PageId;
                        break;
                    }
                }
            }
            else
            {
                nextPageId = flowPages[index + 1].PageId;
            }

            if (!string.IsNullOrEmpty(nextPageId))
            {
                var nextUrl = formNavigationService.GetSubFlowPageUrl(
                    state.CurrentTask!.TaskId,
                    state.ReferenceNumber,
                    flowRoute.FlowId,
                    flowRoute.InstanceId,
                    nextPageId);
                return FormEngineOutcome.Redirect(nextUrl);
            }
        }

        var accumulated = collectionFlowProgressStore.Load(flowRoute.FlowId, flowRoute.InstanceId);
        foreach (var kv in state.Data)
        {
            if (kv.Value?.ToString() == FormEngineConstants.UploadFieldSessionPlaceholder && accumulated.ContainsKey(kv.Key))
                continue;
            accumulated[kv.Key] = kv.Value;
        }

        AppendCollectionItemToSession(flowPages, flowFieldId, flowRoute.InstanceId, accumulated);

        var flow = state.CurrentTask!.Summary?.Flows?.FirstOrDefault(f => f.FlowId == flowRoute.FlowId);
        var taskTitle = state.CurrentTask?.TaskName ?? flow?.Title ?? "Item";
        var successMessage = $"{taskTitle} updated";

        if (state.ApplicationId.HasValue)
        {
            var acc = applicationResponseService.GetAccumulatedFormData();
            if (acc.TryGetValue(flowFieldId, out var collectionValue))
            {
                await applicationResponseService.SaveApplicationResponseAsync(
                    state.ApplicationId.Value,
                    new Dictionary<string, object> { [flowFieldId] = collectionValue },
                    cancellationToken);
            }
        }

        collectionFlowProgressStore.Clear(flowRoute.FlowId, flowRoute.InstanceId);
        var scope = FormRouteParser.HistoryScope(state.ReferenceNumber, state.TaskId, state.CurrentPageId);
        navigationHistoryService.Clear(scope);

        var backToSummary = formNavigationService.GetCollectionFlowSummaryUrl(state.CurrentTask!.TaskId, state.ReferenceNumber);
        return FormEngineOutcome.Redirect(backToSummary, successMessage);
    }

    private async Task<FormEngineOutcome?> SaveDerivedFlowAsync(
        FormEngineWorkState state,
        DerivedFlowRoute derivedRoute,
        CancellationToken cancellationToken)
    {
        var correctTask = state.Template?.TaskGroups?.SelectMany(g => g.Tasks)?.FirstOrDefault(t => t.TaskId == state.TaskId);
        var derivedConfig = FormStepPolicy.GetDerivedFlow(correctTask, derivedRoute.FlowId);
        if (derivedConfig == null)
        {
            logger.LogError("DerivedFlow POST: Could not find derived config for flowId='{FlowId}'", derivedRoute.FlowId);
            return null;
        }

        var currentDerivedPage = FormStepPolicy.ResolvePage(derivedConfig.Pages, derivedRoute.PageId);
        if (currentDerivedPage != null)
        {
            var validation = formValidationOrchestrator.ValidatePage(currentDerivedPage, state.Data, state.Template);
            if (!validation.IsValid)
            {
                var selfUrl = $"/applications/{state.ReferenceNumber}/{state.TaskId}/{state.CurrentPageId}";
                return FormEngineOutcome.Redirect(
                    selfUrl,
                    errors: validation.Errors,
                    persistErrors: true,
                    errorContextKey: state.ErrorContextKey);
            }
        }

        derivedCollectionFlowService.SaveItemDeclaration(
            derivedConfig.FieldId,
            derivedRoute.ItemId,
            state.Data,
            "Signed",
            state.FormData);

        if (state.ApplicationId.HasValue)
        {
            var statusKey = $"{derivedConfig.FieldId}_status_{derivedRoute.ItemId}";
            var dataKey = $"{derivedConfig.FieldId}_data_{derivedRoute.ItemId}";
            var derivedUpdates = new Dictionary<string, object>
            {
                [statusKey] = state.FormData[statusKey],
                [dataKey] = state.FormData[dataKey]
            };
            await applicationResponseService.SaveApplicationResponseAsync(
                state.ApplicationId.Value,
                derivedUpdates,
                cancellationToken);
        }
        else
        {
            logger.LogWarning("DerivedFlow POST: No ApplicationId found, skipping API save");
        }

        var displayName = FormEngineDerivedItems.GetDisplayName(
            derivedConfig,
            derivedRoute.ItemId,
            state.FormData,
            derivedCollectionFlowService);
        var templateMessage = derivedConfig.SignedMessage ?? "Declaration for {displayName} has been signed";
        var successMessage = templateMessage
            .Replace("{displayName}", displayName)
            .Replace("{name}", displayName);

        return FormEngineOutcome.Redirect(
            $"/applications/{state.ReferenceNumber}/{state.TaskId}",
            successMessage);
    }

    private async Task<FormEngineOutcome> CompleteDerivedSummaryFromPageAsync(
        FormEngineWorkState state,
        string? isTaskCompletedValue,
        CancellationToken cancellationToken)
    {
        var isCompleted = ParseCompleted(isTaskCompletedValue);
        if (isCompleted)
        {
            await applicationResponseService.SaveApplicationResponseAsync(
                state.ApplicationId!.Value,
                new Dictionary<string, object> { [$"{state.TaskId}_completed"] = true },
                cancellationToken);

            if (state.CurrentTask != null)
            {
                await applicationStateService.SaveTaskStatusAsync(
                    state.ApplicationId.Value,
                    state.CurrentTask.TaskId,
                    Domain.Models.TaskStatus.Completed);
            }

            logger.LogInformation(
                "POST: About to redirect to task list using RedirectToPage with ReferenceNumber: {ReferenceNumber}",
                state.ReferenceNumber);
            return FormEngineOutcome.RedirectToPage("/FormEngine/RenderForm", new { referenceNumber = state.ReferenceNumber });
        }

        if (state.CurrentTask != null && state.ApplicationId.HasValue)
        {
            var hasAnyData = applicationStateService.CalculateTaskStatus(
                    state.CurrentTask.TaskId,
                    state.Template,
                    state.FormData,
                    state.ApplicationId,
                    state.ApplicationStatus)
                != Domain.Models.TaskStatus.NotStarted;
            var newStatus = hasAnyData ? Domain.Models.TaskStatus.InProgress : Domain.Models.TaskStatus.NotStarted;
            await applicationStateService.SaveTaskStatusAsync(state.ApplicationId.Value, state.CurrentTask.TaskId, newStatus);
        }

        return FormEngineOutcome.RedirectToPage("/FormEngine/RenderForm", new { referenceNumber = state.ReferenceNumber });
    }

    private async Task<FormEngineOutcome> NavigateStandardPageAsync(FormEngineWorkState state)
    {
        if (state.CurrentPage!.ReturnToSummaryPage)
        {
            string? conditionalNextPageId = null;
            var hasConditionalTrigger = false;

            if (state.ConditionalState != null && state.Template != null)
            {
                var visibility = new FormEngineVisibilityEvaluator(
                    state.Template,
                    state.ConditionalState,
                    conditionalLogicOrchestrator,
                    state.CurrentPageId,
                    state.TaskId,
                    logger);
                hasConditionalTrigger = visibility.HasConditionalLogicShowingPages(state.Data);

                logger.LogInformation(
                    "[FLOW DEBUG] ReturnToSummaryPage=true path - hasConditionalTrigger: {HasTrigger}, currentPageId: {PageId}",
                    hasConditionalTrigger,
                    state.CurrentPage.PageId);

                if (hasConditionalTrigger)
                {
                    LogDataPreview(state.Data);
                    var context = new ConditionalLogicContext
                    {
                        CurrentPageId = state.CurrentPageId,
                        CurrentTaskId = state.TaskId,
                        IsClientSide = false,
                        Trigger = "change"
                    };
                    conditionalNextPageId = await conditionalLogicOrchestrator.GetNextPageAsync(
                        state.Template,
                        state.Data,
                        state.CurrentPage.PageId,
                        context);
                    logger.LogInformation("[FLOW DEBUG] GetNextPageAsync returned: {NextPageId}", conditionalNextPageId ?? "null");
                }
            }

            if (hasConditionalTrigger && !string.IsNullOrEmpty(conditionalNextPageId))
            {
                var nextUrl = $"/applications/{state.ReferenceNumber}/{state.CurrentTask!.TaskId}/{conditionalNextPageId}";
                return FormEngineOutcome.Redirect(nextUrl);
            }

            var summaryScope = FormRouteParser.HistoryScope(state.ReferenceNumber, state.TaskId, state.CurrentPageId);
            navigationHistoryService.Clear(summaryScope);
            var summaryUrl = formNavigationService.GetTaskSummaryUrl(state.CurrentTask!.TaskId, state.ReferenceNumber);
            return FormEngineOutcome.Redirect(summaryUrl);
        }

        string? nextPageId = null;
        if (state.ConditionalState != null && state.Template != null)
        {
            logger.LogInformation("[FLOW DEBUG] ReturnToSummaryPage=false path - currentPageId: {PageId}", state.CurrentPage.PageId);
            LogDataPreview(state.Data);
            var context = new ConditionalLogicContext
            {
                CurrentPageId = state.CurrentPageId,
                CurrentTaskId = state.TaskId,
                IsClientSide = false,
                Trigger = "change"
            };
            nextPageId = await conditionalLogicOrchestrator.GetNextPageAsync(
                state.Template,
                state.Data,
                state.CurrentPage.PageId,
                context);
            logger.LogInformation("[FLOW DEBUG] GetNextPageAsync returned: {NextPageId}", nextPageId ?? "null");
        }

        if (!string.IsNullOrEmpty(nextPageId))
        {
            var nextUrl = $"/applications/{state.ReferenceNumber}/{state.CurrentTask!.TaskId}/{nextPageId}";
            return FormEngineOutcome.Redirect(nextUrl);
        }

        var sequentialNextPage = FormStepPolicy.GetNextPage(state.CurrentTask!.Pages, state.CurrentPage.PageId);
        if (sequentialNextPage != null)
        {
            var nextUrl = $"/applications/{state.ReferenceNumber}/{state.CurrentTask.TaskId}/{sequentialNextPage.PageId}";
            return FormEngineOutcome.Redirect(nextUrl);
        }

        var summaryFallbackScope = FormRouteParser.HistoryScope(state.ReferenceNumber, state.TaskId, state.CurrentPageId);
        navigationHistoryService.Clear(summaryFallbackScope);
        var fallbackUrl = formNavigationService.GetTaskSummaryUrl(state.CurrentTask.TaskId, state.ReferenceNumber);
        return FormEngineOutcome.Redirect(fallbackUrl);
    }

    private async Task<FormEngineOutcome> CompleteDerivedSummaryFallbackAsync(
        FormEngineWorkState state,
        string? isTaskCompletedValue,
        CancellationToken cancellationToken)
    {
        var isCompleted = ParseCompleted(isTaskCompletedValue);
        if (isCompleted)
        {
            var derivedFlows = state.CurrentTask?.Summary?.DerivedFlows;
            var errorLines = new List<string>();

            if (derivedFlows != null && derivedFlows.Count > 0)
            {
                foreach (var derivedFlow in derivedFlows)
                {
                    var derivedItems = derivedCollectionFlowService.GenerateItemsFromSourceField(
                        derivedFlow.SourceFieldId,
                        state.FormData,
                        derivedFlow);

                    if (derivedItems.Count == 0)
                    {
                        var errorMessage = !string.IsNullOrEmpty(derivedFlow.NoItemsErrorMessage)
                            ? derivedFlow.NoItemsErrorMessage
                            : $"You need to add at least one item before signing the {derivedFlow.Title}";
                        errorLines.Add(errorMessage);
                        continue;
                    }

                    var statuses = derivedCollectionFlowService.GetItemStatuses(derivedFlow.FieldId, state.FormData);
                    var unsignedItems = derivedItems
                        .Where(item => !statuses.ContainsKey(item.Id) || statuses[item.Id] != "Signed")
                        .ToList();

                    foreach (var item in unsignedItems)
                    {
                        var displayName = FormEngineDerivedItems.GetDisplayName(
                            derivedFlow,
                            item.Id,
                            state.FormData,
                            derivedCollectionFlowService);
                        var errorMessage = !string.IsNullOrEmpty(derivedFlow.UnsignedItemErrorMessage)
                            ? derivedFlow.UnsignedItemErrorMessage.Replace("{sourceName}", displayName)
                            : $"You need to sign the declaration for {displayName}";
                        errorLines.Add(errorMessage);
                    }
                }
            }

            if (errorLines.Count > 0)
            {
                var errors = new List<FormValidationError>
                {
                    new(string.Empty, "You cannot mark this section as complete:")
                };
                errors.AddRange(errorLines.Select(line => new FormValidationError(string.Empty, line)));

                return FormEngineOutcome.Stay(
                    formState: FormState.DerivedCollectionFlowSummary,
                    errors: errors,
                    clearModelState: true,
                    isTaskCompleted: false,
                    reloadFormData: true);
            }
        }

        if (state.ApplicationId.HasValue && state.CurrentTask != null)
        {
            if (isCompleted)
            {
                await applicationStateService.SaveTaskStatusAsync(
                    state.ApplicationId.Value,
                    state.CurrentTask.TaskId,
                    Domain.Models.TaskStatus.Completed);
            }
            else
            {
                var hasAnyData = applicationStateService.CalculateTaskStatus(
                        state.CurrentTask.TaskId,
                        state.Template,
                        state.FormData,
                        state.ApplicationId,
                        state.ApplicationStatus)
                    != Domain.Models.TaskStatus.NotStarted;
                var newStatus = hasAnyData ? Domain.Models.TaskStatus.InProgress : Domain.Models.TaskStatus.NotStarted;
                await applicationStateService.SaveTaskStatusAsync(
                    state.ApplicationId.Value,
                    state.CurrentTask.TaskId,
                    newStatus);
            }
        }

        var taskListUrl = formNavigationService.GetTaskListUrl(state.ReferenceNumber);
        return FormEngineOutcome.Redirect(taskListUrl);
    }

    private bool IsExistingCollectionItem(string fieldId, string instanceId)
    {
        var accumulated = applicationResponseService.GetAccumulatedFormData();
        var items = FormEngineCollectionItems.Read(accumulated, fieldId);
        return items.Any(item => item.TryGetValue("id", out var id) && id?.ToString() == instanceId);
    }

    private void AppendCollectionItemToSession(
        List<PageModel> pages,
        string fieldId,
        string instanceId,
        Dictionary<string, object> itemData)
    {
        var acc = applicationResponseService.GetAccumulatedFormData();
        var list = FormEngineCollectionItems.Read(acc, fieldId);

        var idx = list.FindIndex(x => x.TryGetValue("id", out var id) && id?.ToString() == instanceId);
        Dictionary<string, object> item;

        if (idx >= 0)
        {
            item = new Dictionary<string, object>(list[idx]);
            foreach (var kvp in itemData)
            {
                if (kvp.Value?.ToString() == FormEngineConstants.UploadFieldSessionPlaceholder
                    && item.TryGetValue(kvp.Key, out var existingVal)
                    && existingVal != null
                    && existingVal.ToString()!.StartsWith('[')
                    && existingVal.ToString()!.Contains("\"id\""))
                {
                    continue;
                }

                item[kvp.Key] = kvp.Value;
            }
        }
        else
        {
            item = new Dictionary<string, object>();
            foreach (var page in pages)
            {
                foreach (var field in page.Fields)
                {
                    var key = field.FieldId;
                    if (!itemData.TryGetValue(key, out var value))
                        continue;
                    if (value?.ToString() == FormEngineConstants.UploadFieldSessionPlaceholder)
                        continue;
                    item[key] = value;
                }
            }

            item["id"] = instanceId;
        }

        item["id"] = instanceId;

        if (idx >= 0)
            list[idx] = item;
        else
            list.Add(item);

        var serialized = JsonSerializer.Serialize(list);
        applicationResponseService.AccumulateFormData(new Dictionary<string, object> { [fieldId] = serialized });
    }

    private void LogDataPreview(Dictionary<string, object> data)
    {
        logger.LogInformation("[FLOW DEBUG] Data before calling GetNextPageAsync:");
        foreach (var kv in data.Take(10))
            logger.LogInformation("[FLOW DEBUG] Data[{Key}] = {Value}", kv.Key, kv.Value?.ToString() ?? "null");
    }

    private static bool ParseCompleted(string? value) =>
        !string.IsNullOrEmpty(value)
        && (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase));
}
