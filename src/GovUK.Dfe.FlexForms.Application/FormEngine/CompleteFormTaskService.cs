using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Validation;
using GovUK.Dfe.FlexForms.Domain.FormEngine;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.Extensions.Logging;
using TaskModel = GovUK.Dfe.FlexForms.Domain.Models.Task;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

/// <summary>
/// Marks a task complete (or reverts it) from the task-summary POST.
/// </summary>
public interface ICompleteFormTask
{
    Task<FormEngineOutcome> ExecuteAsync(FormEngineWorkState state, CancellationToken cancellationToken = default);
}

public sealed class CompleteFormTaskService(
    IApplicationStateService applicationStateService,
    IFieldRequirementService fieldRequirementService,
    IConditionalLogicOrchestrator conditionalLogicOrchestrator,
    ILogger<CompleteFormTaskService> logger) : ICompleteFormTask
{
    public async Task<FormEngineOutcome> ExecuteAsync(
        FormEngineWorkState state,
        CancellationToken cancellationToken = default)
    {
        var conditionalState = await ApplyTaskSummaryConditionalLogicAsync(state);
        var visibility = new FormEngineVisibilityEvaluator(
            state.Template,
            conditionalState,
            conditionalLogicOrchestrator,
            state.CurrentPageId,
            state.TaskId,
            logger);

        if (state.CurrentTask != null && state.ApplicationId.HasValue)
        {
            if (state.IsTaskCompleted)
            {
                var errorLines = CollectCompletionErrors(state, visibility);
                if (errorLines.Count > 0)
                {
                    var errorMessage =
                        "You cannot mark this section as complete because some required questions have not been answered:\n" +
                        string.Join("\n", errorLines);
                    return FormEngineOutcome.Stay(
                        formState: FormState.TaskSummary,
                        errors: [new FormValidationError(string.Empty, errorMessage)],
                        clearModelState: true,
                        isTaskCompleted: false,
                        conditionalState: conditionalState);
                }

                await applicationStateService.SaveTaskStatusAsync(
                    state.ApplicationId.Value,
                    state.CurrentTask.TaskId,
                    Domain.Models.TaskStatus.Completed);
            }
            else
            {
                var currentStatus = applicationStateService.CalculateTaskStatus(
                    state.CurrentTask.TaskId,
                    state.Template!,
                    state.FormData,
                    state.ApplicationId,
                    state.ApplicationStatus);
                if (currentStatus == Domain.Models.TaskStatus.Completed)
                {
                    var calculatedStatus = HasAnyTaskData(state.CurrentTask, state.FormData)
                        ? Domain.Models.TaskStatus.InProgress
                        : Domain.Models.TaskStatus.NotStarted;
                    await applicationStateService.SaveTaskStatusAsync(
                        state.ApplicationId.Value,
                        state.CurrentTask.TaskId,
                        calculatedStatus);
                }
            }
        }

        return FormEngineOutcome.Redirect($"/applications/{state.ReferenceNumber}");
    }

    private async Task<FormConditionalState> ApplyTaskSummaryConditionalLogicAsync(FormEngineWorkState state)
    {
        try
        {
            if (state.Template?.ConditionalLogic != null && state.Template.ConditionalLogic.Any())
            {
                var context = new ConditionalLogicContext
                {
                    CurrentPageId = state.CurrentPageId,
                    CurrentTaskId = state.TaskId,
                    IsClientSide = false,
                    Trigger = "task_summary_validation"
                };
                return await conditionalLogicOrchestrator.ApplyConditionalLogicAsync(
                    state.Template,
                    state.FormData,
                    context);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error applying conditional logic in task summary validation");
        }

        return new FormConditionalState();
    }

    private List<string> CollectCompletionErrors(FormEngineWorkState state, FormEngineVisibilityEvaluator visibility)
    {
        var errorLines = new List<string>();
        var missingFieldsWithMessages = fieldRequirementService.GetMissingRequiredFieldsWithMessages(
            state.CurrentTask!,
            state.Template!,
            state.FormData,
            visibility.IsFieldHidden);

        errorLines.AddRange(missingFieldsWithMessages.Values);

        if (state.CurrentTask!.Summary?.Mode?.Equals("multiCollectionFlow", StringComparison.OrdinalIgnoreCase) != true
            || state.CurrentTask.Summary.Flows == null
            || !state.CurrentTask.Summary.Flows.Any())
        {
            return errorLines;
        }

        foreach (var flow in state.CurrentTask.Summary.Flows)
        {
            var items = FormEngineCollectionItems.Read(state.FormData, flow.FieldId);
            var requiredMin = flow.MinItems ?? 1;
            if (items.Count < requiredMin)
            {
                var flowTitle = string.IsNullOrWhiteSpace(flow.Title)
                    ? (string.IsNullOrWhiteSpace(state.CurrentTask.TaskName) ? "this section" : state.CurrentTask.TaskName)
                    : flow.Title;
                errorLines.Add($"• Add at least {requiredMin} item(s) to {flowTitle}");
                logger.LogInformation(
                    "Collection flow '{FlowId}' requires at least {MinItems} items but has {Count}",
                    flow.FlowId,
                    requiredMin,
                    items.Count);
            }

            if (flow.Pages == null || items.Count == 0)
                continue;

            foreach (var item in items)
            {
                var requiredFieldIds = flow.Pages
                    .Where(p => p?.Fields != null)
                    .SelectMany(p => p.Fields)
                    .Where(f => fieldRequirementService.IsFieldRequired(f, state.Template!))
                    .Select(f => f.FieldId)
                    .ToList();
                visibility.EnsureItemFieldVisibility(item, requiredFieldIds);

                var itemHasMissingFields = flow.Pages
                    .Where(page => page?.Fields != null)
                    .SelectMany(page => page.Fields)
                    .Where(field => fieldRequirementService.IsFieldRequired(field, state.Template!))
                    .Where(field => !visibility.IsFieldHiddenForItem(field.FieldId, item))
                    .Any(field =>
                    {
                        var hasValue = item.TryGetValue(field.FieldId, out var val)
                                       && val != null
                                       && !string.IsNullOrWhiteSpace(val.ToString());
                        return !hasValue;
                    });

                if (!itemHasMissingFields)
                    continue;

                var flowTitle = string.IsNullOrWhiteSpace(flow.Title)
                    ? (string.IsNullOrWhiteSpace(state.CurrentTask.TaskName) ? "this section" : state.CurrentTask.TaskName)
                    : flow.Title;
                errorLines.Add($"Complete all required questions for each item in {flowTitle}");
                logger.LogInformation("Collection flow '{FlowId}' has an item with incomplete required fields", flow.FlowId);
                break;
            }
        }

        return errorLines;
    }

    private static bool HasAnyTaskData(TaskModel task, Dictionary<string, object> formData)
    {
        var taskFieldIds = new List<string>();
        if (task.Pages != null)
        {
            taskFieldIds.AddRange(task.Pages.SelectMany(p => p.Fields).Select(f => f.FieldId));
        }

        if (task.Summary?.Mode?.Equals(FormStepPolicy.MultiCollectionFlowMode, StringComparison.OrdinalIgnoreCase) == true
            && task.Summary.Flows != null)
        {
            taskFieldIds.AddRange(task.Summary.Flows.Select(f => f.FieldId));
        }

        return taskFieldIds.Any(fieldId =>
            formData.ContainsKey(fieldId) && !string.IsNullOrWhiteSpace(formData[fieldId]?.ToString()));
    }
}
