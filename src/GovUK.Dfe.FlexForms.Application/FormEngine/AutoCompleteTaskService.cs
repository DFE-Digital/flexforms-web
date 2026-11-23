using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.FormEngine;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

/// <summary>
/// Result of evaluating a task for automatic completion.
/// </summary>
/// <param name="IsAutomatic">True when the task resolves to automatic completion and can be judged.</param>
/// <param name="IsComplete">True when the task currently meets the completion rules.</param>
public sealed record AutoCompleteTaskResult(bool IsAutomatic, bool IsComplete, IReadOnlyList<string> Outstanding)
{
    public static readonly AutoCompleteTaskResult NotApplicable = new(false, false, []);
}

/// <summary>
/// Opt-in automatic task completion. Only acts on tasks whose resolved taskCompletion is "automatic".
/// </summary>
public interface IAutoCompleteTask
{
    Task<AutoCompleteTaskResult> EvaluateAsync(
        FormEngineWorkState state,
        Dictionary<string, object> formData,
        CancellationToken cancellationToken = default);
}

public sealed class AutoCompleteTaskService(
    IApplicationStateService applicationStateService,
    IFieldRequirementService fieldRequirementService,
    IDerivedCollectionFlowService derivedCollectionFlowService,
    IConditionalLogicOrchestrator conditionalLogicOrchestrator,
    ILogger<AutoCompleteTaskService> logger) : IAutoCompleteTask
{
    public async Task<AutoCompleteTaskResult> EvaluateAsync(
        FormEngineWorkState state,
        Dictionary<string, object> formData,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var task = state.CurrentTask;
            var template = state.Template;
            if (task == null || template == null || !state.ApplicationId.HasValue)
                return AutoCompleteTaskResult.NotApplicable;

            if (!TaskCompletionPolicy.IsAutomatic(task, template))
                return AutoCompleteTaskResult.NotApplicable;

            if (!state.IsEditable
                || state.ApplicationStatus.Equals("Submitted", StringComparison.OrdinalIgnoreCase))
                return AutoCompleteTaskResult.NotApplicable;

            if (!TaskCompletionRules.HasCompletionCriteria(task, template, fieldRequirementService))
                return AutoCompleteTaskResult.NotApplicable;

            var conditionalState = await ApplyConditionalLogicAsync(state, template, formData);
            var visibility = new FormEngineVisibilityEvaluator(
                template,
                conditionalState,
                conditionalLogicOrchestrator,
                state.CurrentPageId,
                task.TaskId,
                logger);

            var errors = TaskCompletionRules.CollectStandardAndCollectionErrors(
                task, template, formData, fieldRequirementService, visibility, logger);
            errors.AddRange(TaskCompletionRules.CollectDerivedErrors(task, formData, derivedCollectionFlowService));

            var isComplete = errors.Count == 0;
            var currentStatus = applicationStateService.CalculateTaskStatus(
                task.TaskId, template, formData, state.ApplicationId, state.ApplicationStatus);
            var isCurrentlyCompleted = currentStatus == Domain.Models.TaskStatus.Completed;

            if (isComplete && !isCurrentlyCompleted)
            {
                await applicationStateService.SaveTaskStatusAsync(
                    state.ApplicationId.Value, task.TaskId, Domain.Models.TaskStatus.Completed);
            }
            else if (!isComplete && isCurrentlyCompleted)
            {
                var revertedStatus = CompleteFormTaskService.HasAnyTaskData(task, formData)
                    ? Domain.Models.TaskStatus.InProgress
                    : Domain.Models.TaskStatus.NotStarted;
                await applicationStateService.SaveTaskStatusAsync(
                    state.ApplicationId.Value, task.TaskId, revertedStatus);
            }

            return new AutoCompleteTaskResult(true, isComplete, errors);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Automatic task completion failed for task {TaskId}", state.CurrentTask?.TaskId);
            return AutoCompleteTaskResult.NotApplicable;
        }
    }

    private async Task<FormConditionalState> ApplyConditionalLogicAsync(
        FormEngineWorkState state,
        FormTemplate template,
        Dictionary<string, object> formData)
    {
        try
        {
            if (template.ConditionalLogic != null && template.ConditionalLogic.Any())
            {
                var context = new ConditionalLogicContext
                {
                    CurrentPageId = state.CurrentPageId,
                    CurrentTaskId = state.TaskId,
                    IsClientSide = false,
                    Trigger = "task_summary_validation"
                };
                return await conditionalLogicOrchestrator.ApplyConditionalLogicAsync(template, formData, context);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error applying conditional logic for automatic task completion");
        }

        return new FormConditionalState();
    }
}
