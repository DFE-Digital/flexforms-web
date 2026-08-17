using GovUK.Dfe.FlexForms.Domain.Models;
using TaskModel = GovUK.Dfe.FlexForms.Domain.Models.Task;

namespace GovUK.Dfe.FlexForms.Domain.FormEngine;

/// <summary>
/// Pure rules for which engine step to show and how to walk page lists.
/// </summary>
public static class FormStepPolicy
{
    public const string MultiCollectionFlowMode = "multiCollectionFlow";
    public const string DerivedCollectionFlowMode = "derivedCollectionFlow";

    public static bool IsCollectionFlowPage(string? pageId) => FormRouteParser.LooksLikeCollectionFlow(pageId);

    public static bool IsFormPage(string? pageId) =>
        !string.IsNullOrEmpty(pageId) && !IsCollectionFlowPage(pageId);

    public static bool IsTaskSummary(string? taskId, string? pageId) =>
        !string.IsNullOrEmpty(taskId) && string.IsNullOrEmpty(pageId);

    public static bool IsTaskList(string? taskId, string? pageId) =>
        string.IsNullOrEmpty(taskId) && string.IsNullOrEmpty(pageId);

    public static bool IsApplicationPreview(string? pageId) => false;

    public static bool IsCollectionFlowSummary(TaskModel? task) =>
        task?.Summary?.Mode?.Equals(MultiCollectionFlowMode, StringComparison.OrdinalIgnoreCase) == true;

    public static bool IsDerivedCollectionFlowSummary(TaskModel? task) =>
        task?.Summary?.Mode?.Equals(DerivedCollectionFlowMode, StringComparison.OrdinalIgnoreCase) == true;

    public static bool IsInSubFlow(string flowId, string? pageId) =>
        !string.IsNullOrEmpty(pageId)
        && pageId.StartsWith($"{FormRouteParser.CollectionFlowSegment}/{flowId}", StringComparison.OrdinalIgnoreCase);

    public static MultiCollectionFlowConfiguration? GetCollectionFlow(TaskModel? task, string flowId) =>
        task?.Summary?.Flows?.FirstOrDefault(f => f.FlowId == flowId);

    public static DerivedCollectionFlowConfiguration? GetDerivedFlow(TaskModel? task, string derivedFlowId) =>
        task?.Summary?.DerivedFlows?.FirstOrDefault(f => f.FlowId == derivedFlowId);

    public static IReadOnlyList<Page>? GetCollectionFlowPages(TaskModel? task, string flowId) =>
        GetCollectionFlow(task, flowId)?.Pages;

    public static string? GetCollectionFlowFieldId(TaskModel? task, string flowId) =>
        GetCollectionFlow(task, flowId)?.FieldId;

    public static Page? ResolvePage(IReadOnlyList<Page>? pages, string? pageId)
    {
        if (pages == null || pages.Count == 0)
            return null;

        return string.IsNullOrEmpty(pageId)
            ? pages[0]
            : pages.FirstOrDefault(p => p.PageId == pageId);
    }

    public static Page? GetNextPage(IReadOnlyList<Page>? pages, string currentPageId)
    {
        var index = IndexOfPage(pages, currentPageId);
        if (index == -1 || pages is null || index >= pages.Count - 1)
            return null;

        return pages[index + 1];
    }

    public static bool IsLastPage(IReadOnlyList<Page>? pages, string currentPageId)
    {
        var index = IndexOfPage(pages, currentPageId);
        return pages == null || pages.Count == 0 || index == -1 || index >= pages.Count - 1;
    }

    public static int IndexOfPage(IReadOnlyList<Page>? pages, string currentPageId)
    {
        if (pages == null)
            return -1;

        for (var i = 0; i < pages.Count; i++)
        {
            if (pages[i].PageId == currentPageId)
                return i;
        }

        return -1;
    }
}
