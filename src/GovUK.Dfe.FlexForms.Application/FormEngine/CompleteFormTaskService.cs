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
            if (state.Template != null
                && TaskCompletionPolicy.IsAutomatic(state.CurrentTask, state.Template)
                && TaskCompletionRules.HasCompletionCriteria(state.CurrentTask, state.Template, fieldRequirementService))
            {
                // Automatic tasks are completed by AutoCompleteTaskService, not by the (hidden) checkbox.
                return FormEngineOutcome.Redirect($"/applications/{state.ReferenceNumber}");
            }

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

    private List<string> CollectCompletionErrors(FormEngineWorkState state, FormEngineVisibilityEvaluator visibility) =>
        TaskCompletionRules.CollectStandardAndCollectionErrors(
            state.CurrentTask!,
            state.Template!,
            state.FormData,
            fieldRequirementService,
            visibility,
            logger);

    internal static bool HasAnyTaskData(TaskModel task, Dictionary<string, object> formData)
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
