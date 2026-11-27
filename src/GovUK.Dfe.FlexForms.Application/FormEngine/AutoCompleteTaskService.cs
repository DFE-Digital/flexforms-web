using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.FormEngine;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.Extensions.Logging;
using TaskModel = GovUK.Dfe.FlexForms.Domain.Models.Task;

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
        var task = state.CurrentTask;
        var template = state.Template;
        if (task == null || template == null || !state.ApplicationId.HasValue)
            return AutoCompleteTaskResult.NotApplicable;

        if (!state.IsEditable
            || state.ApplicationStatus.Equals("Submitted", StringComparison.OrdinalIgnoreCase))
            return AutoCompleteTaskResult.NotApplicable;

        var result = await EvaluateTaskAsync(state, template, task, state.CurrentPageId, formData);

        foreach (var dependent in GetDependentDerivedTasks(template, task))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EvaluateTaskAsync(state, template, dependent, string.Empty, formData);
        }

        return result;
    }

    private async Task<AutoCompleteTaskResult> EvaluateTaskAsync(
        FormEngineWorkState state,
        FormTemplate template,
        TaskModel task,
        string pageId,
        Dictionary<string, object> formData)
    {
        try
        {
            if (!TaskCompletionPolicy.IsAutomatic(task, template))
                return AutoCompleteTaskResult.NotApplicable;

            if (!TaskCompletionRules.HasCompletionCriteria(task, template, fieldRequirementService))
                return AutoCompleteTaskResult.NotApplicable;

            var conditionalState = await ApplyConditionalLogicAsync(template, task.TaskId, pageId, formData);
            var visibility = new FormEngineVisibilityEvaluator(
                template,
                conditionalState,
                conditionalLogicOrchestrator,
                pageId,
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
                    state.ApplicationId!.Value, task.TaskId, Domain.Models.TaskStatus.Completed);
            }
            else if (!isComplete && isCurrentlyCompleted)
            {
                var revertedStatus = CompleteFormTaskService.HasAnyTaskData(task, formData)
                    ? Domain.Models.TaskStatus.InProgress
                    : Domain.Models.TaskStatus.NotStarted;
                await applicationStateService.SaveTaskStatusAsync(
                    state.ApplicationId!.Value, task.TaskId, revertedStatus);
            }

            return new AutoCompleteTaskResult(true, isComplete, errors);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Automatic task completion failed for task {TaskId}", task.TaskId);
            return AutoCompleteTaskResult.NotApplicable;
        }
    }

    private static List<TaskModel> GetDependentDerivedTasks(FormTemplate template, TaskModel sourceTask)
    {
        var sourceFieldIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (sourceTask.Pages != null)
        {
            foreach (var page in sourceTask.Pages.Where(p => p?.Fields != null))
                foreach (var field in page.Fields)
                    sourceFieldIds.Add(field.FieldId);
        }

        if (sourceTask.Summary?.Flows != null)
        {
            foreach (var flow in sourceTask.Summary.Flows.Where(f => !string.IsNullOrEmpty(f.FieldId)))
                sourceFieldIds.Add(flow.FieldId);
        }

        if (sourceFieldIds.Count == 0)
            return [];

        return template.TaskGroups
            .Where(g => g?.Tasks != null)
            .SelectMany(g => g.Tasks)
            .Where(t => t.TaskId != sourceTask.TaskId)
            .Where(t => t.Summary?.DerivedFlows?.Any(d =>
                !string.IsNullOrEmpty(d.SourceFieldId) && sourceFieldIds.Contains(d.SourceFieldId)) == true)
            .ToList();
    }

    private async Task<FormConditionalState> ApplyConditionalLogicAsync(
        FormTemplate template,
        string taskId,
        string pageId,
        Dictionary<string, object> formData)
    {
        try
        {
            if (template.ConditionalLogic != null && template.ConditionalLogic.Any())
            {
                var context = new ConditionalLogicContext
                {
                    CurrentPageId = pageId,
                    CurrentTaskId = taskId,
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
