using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Validation;
using GovUK.Dfe.FlexForms.Domain.FormEngine;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.Extensions.Logging;
using PageModel = GovUK.Dfe.FlexForms.Domain.Models.Page;
using TaskModel = GovUK.Dfe.FlexForms.Domain.Models.Task;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

/// <summary>
/// The result of answering one page of an unsaved template.
/// </summary>
/// <param name="Data">The answers after this page, including the ones just posted.</param>
/// <param name="NextPageId">The page to show next, or null to go to the task's summary.</param>
public sealed record DraftPageOutcome(
    Dictionary<string, object> Data,
    IReadOnlyList<FormValidationError> Errors,
    FormConditionalState ConditionalState,
    string? NextPageId)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Runs an unsaved template through the form engine's binding, conditional logic, validation and navigation,
/// with answers held by the caller instead of an application.
/// </summary>
public interface IDraftFormPreview
{
    Task<FormConditionalState> EvaluateAsync(FormTemplate template, Dictionary<string, object> data, string taskId, string pageId);

    FormEngineVisibilityEvaluator Visibility(FormTemplate template, FormConditionalState conditionalState, string taskId, string pageId);

    /// <param name="pages">The pages the page belongs to, in order: the task's pages, or a list's or repeated pages' pages.</param>
    /// <param name="isTaskPage">Task pages follow the page's navigation setting. Pages of lists go to the next page shown.</param>
    Task<DraftPageOutcome> SubmitPageAsync(
        FormTemplate template,
        TaskModel task,
        PageModel page,
        IReadOnlyList<PageModel> pages,
        bool isTaskPage,
        Dictionary<string, object> data,
        IReadOnlyDictionary<string, IReadOnlyList<string>> postedFields);

    /// <summary>
    /// Why the task can't be marked as complete yet, in the words the form engine uses. Empty when it can.
    /// </summary>
    Task<IReadOnlyList<string>> CompletionProblemsAsync(FormTemplate template, TaskModel task, Dictionary<string, object> data);
}

public sealed class DraftFormPreviewService(
    IPostedFormDataBinder postedFormDataBinder,
    IFormValidationOrchestrator formValidationOrchestrator,
    IConditionalLogicOrchestrator conditionalLogicOrchestrator,
    IFieldRequirementService fieldRequirementService,
    IDerivedCollectionFlowService derivedCollectionFlowService,
    ILogger<DraftFormPreviewService> logger) : IDraftFormPreview
{
    public Task<FormConditionalState> EvaluateAsync(FormTemplate template, Dictionary<string, object> data, string taskId, string pageId) =>
        FormEngineConditionalLogic.ApplyAsync(template, new Dictionary<string, object>(data), data, conditionalLogicOrchestrator, pageId, taskId, "load", logger);

    public FormEngineVisibilityEvaluator Visibility(FormTemplate template, FormConditionalState conditionalState, string taskId, string pageId) =>
        new(template, conditionalState, conditionalLogicOrchestrator, pageId, taskId, logger);

    public async Task<DraftPageOutcome> SubmitPageAsync(
        FormTemplate template,
        TaskModel task,
        PageModel page,
        IReadOnlyList<PageModel> pages,
        bool isTaskPage,
        Dictionary<string, object> data,
        IReadOnlyDictionary<string, IReadOnlyList<string>> postedFields)
    {
        var pageData = postedFormDataBinder.Bind(postedFields, new Dictionary<string, object>(data));
        var conditionalState = await FormEngineConditionalLogic.ApplyAsync(
            template, pageData, pageData, conditionalLogicOrchestrator, page.PageId, task.TaskId, "change", logger);
        postedFormDataBinder.ApplyDateParts(postedFields, pageData);

        var visibility = Visibility(template, conditionalState, task.TaskId, page.PageId);
        var errors = formValidationOrchestrator.ValidatePage(page, pageData, template).Errors
            .Where(error => string.IsNullOrEmpty(error.FieldKey) || !visibility.IsFieldHidden(error.FieldKey))
            .ToList();
        if (errors.Count > 0)
            return new DraftPageOutcome(pageData, errors, conditionalState, null);

        var nextPageId = isTaskPage
            ? await NextTaskPageAsync(template, task, page, pages, pageData, visibility)
            : NextVisiblePage(page, pages, visibility);
        return new DraftPageOutcome(pageData, [], conditionalState, nextPageId);
    }

    public async Task<IReadOnlyList<string>> CompletionProblemsAsync(FormTemplate template, TaskModel task, Dictionary<string, object> data)
    {
        var conditionalState = await FormEngineConditionalLogic.ApplyAsync(
            template, new Dictionary<string, object>(data), data, conditionalLogicOrchestrator, string.Empty, task.TaskId, "task_summary_validation", logger);
        var visibility = Visibility(template, conditionalState, task.TaskId, string.Empty);
        var problems = new List<string>();

        if (task.Pages is { Count: > 0 })
            problems.AddRange(fieldRequirementService.GetMissingRequiredFieldsWithMessages(task, template, data, visibility.IsFieldHidden).Values);

        var taskTitle = string.IsNullOrWhiteSpace(task.TaskName) ? "this section" : task.TaskName;
        foreach (var flow in task.Summary?.Flows ?? [])
        {
            var flowTitle = string.IsNullOrWhiteSpace(flow.Title) ? taskTitle : flow.Title;
            var items = FormEngineCollectionItems.Read(data, flow.FieldId);
            var requiredMin = flow.MinItems ?? 1;
            if (items.Count < requiredMin)
                problems.Add($"Add at least {requiredMin} item(s) to {flowTitle}");

            var requiredFields = (flow.Pages ?? [])
                .SelectMany(flowPage => flowPage.Fields ?? [])
                .Where(field => fieldRequirementService.IsFieldRequired(field, template))
                .ToList();
            var incomplete = items.Any(item =>
            {
                visibility.EnsureItemFieldVisibility(item, requiredFields.Select(field => field.FieldId));
                return requiredFields
                    .Where(field => !visibility.IsFieldHiddenForItem(field.FieldId, item))
                    .Any(field => !item.TryGetValue(field.FieldId, out var value) || string.IsNullOrWhiteSpace(value?.ToString()));
            });
            if (incomplete)
                problems.Add($"Complete all required questions for each item in {flowTitle}");
        }

        foreach (var flow in task.Summary?.DerivedFlows ?? [])
        {
            var items = derivedCollectionFlowService.GenerateItemsFromSourceField(flow.SourceFieldId, data, flow);
            if (items.Count == 0)
            {
                problems.Add(!string.IsNullOrEmpty(flow.NoItemsErrorMessage)
                    ? flow.NoItemsErrorMessage
                    : $"You need to add at least one item before signing the {flow.Title}");
                continue;
            }

            var statuses = derivedCollectionFlowService.GetItemStatuses(flow.FieldId, data);
            foreach (var item in items.Where(item => !statuses.TryGetValue(item.Id, out var status) || status != "Signed"))
            {
                var displayName = FormEngineDerivedItems.GetDisplayName(flow, item.Id, data, derivedCollectionFlowService);
                problems.Add(!string.IsNullOrEmpty(flow.UnsignedItemErrorMessage)
                    ? flow.UnsignedItemErrorMessage.Replace("{sourceName}", displayName)
                    : $"You need to sign the declaration for {displayName}");
            }
        }

        return problems;
    }

    // Mirrors the form engine: the last page shown always goes to the summary, otherwise the page's navigation setting decides.
    private async Task<string?> NextTaskPageAsync(
        FormTemplate template,
        TaskModel task,
        PageModel page,
        IReadOnlyList<PageModel> pages,
        Dictionary<string, object> data,
        FormEngineVisibilityEvaluator visibility)
    {
        string? nextVisible;
        if (template.ConditionalLogic?.Any() == true)
        {
            var context = new ConditionalLogicContext { CurrentPageId = page.PageId, CurrentTaskId = task.TaskId, IsClientSide = false, Trigger = "change" };
            var candidate = await conditionalLogicOrchestrator.GetNextPageAsync(template, data, page.PageId, context);
            nextVisible = pages.Any(item => item.PageId == candidate) ? candidate : null;
        }
        else
        {
            nextVisible = FormStepPolicy.GetNextPage(pages, page.PageId)?.PageId;
        }

        if (string.IsNullOrEmpty(nextVisible))
            return null;

        return PageNavigationPolicy.Resolve(page) switch
        {
            NavigationAfterSave.Summary => null,
            NavigationAfterSave.Branch => visibility.GetNextPageRevealedByCurrentPageFields(page, data, pages),
            _ => nextVisible
        };
    }

    private static string? NextVisiblePage(PageModel page, IReadOnlyList<PageModel> pages, FormEngineVisibilityEvaluator visibility)
    {
        var index = FormStepPolicy.IndexOfPage(pages, page.PageId);
        for (var i = index + 1; index >= 0 && i < pages.Count; i++)
        {
            if (!visibility.IsPageHidden(pages[i].PageId))
                return pages[i].PageId;
        }

        return null;
    }
}
