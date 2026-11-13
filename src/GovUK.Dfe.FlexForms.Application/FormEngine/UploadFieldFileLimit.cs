using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.FormEngine;
using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

/// <summary>
/// Maximum number of files an upload question accepts: <see cref="ComplexFieldConfiguration.MaxSelections"/>
/// on the tenant's complex field (FormEngine:ComplexFields). 0 means no limit.
/// </summary>
public static class UploadFieldFileLimit
{
    public static int Resolve(
        FormTemplate? template,
        string fieldId,
        IComplexFieldConfigurationService complexFieldConfigurationService,
        string? taskId = null,
        string? flowId = null)
    {
        var complexFieldId = FindField(template, fieldId, taskId, flowId)?.ComplexField?.Id;
        if (string.IsNullOrWhiteSpace(complexFieldId))
            return 0;

        return Math.Max(0, complexFieldConfigurationService.GetConfiguration(complexFieldId)?.MaxSelections ?? 0);
    }

    /// <summary>
    /// The collection or derived flow the request is in. Uploads on derived-flow pages only carry
    /// the flow in the page route (<c>{flowId}/derived/{itemId}/{pageId}</c>).
    /// </summary>
    public static string? CurrentFlowId(FormEngineWorkState state)
    {
        if (!string.IsNullOrEmpty(state.FlowId))
            return state.FlowId;

        if (!string.IsNullOrEmpty(state.DerivedFlowId))
            return state.DerivedFlowId;

        if (FormRouteParser.TryParseDerivedFlow(state.CurrentPageId, out var derived))
            return derived.FlowId;

        return FormRouteParser.TryParseCollectionFlow(state.CurrentPageId, out var collection)
            ? collection.FlowId
            : null;
    }

    /// <summary>
    /// Finds a field, looking first in the current flow (or the task's own pages), then the rest of the task,
    /// then the whole template. Field ids may repeat across collection flows, so the current context wins.
    /// </summary>
    public static Field? FindField(FormTemplate? template, string fieldId, string? taskId = null, string? flowId = null)
    {
        if (template?.TaskGroups is null || string.IsNullOrWhiteSpace(fieldId))
            return null;

        var tasks = template.TaskGroups.SelectMany(group => group.Tasks ?? []).ToList();
        var task = string.IsNullOrEmpty(taskId)
            ? null
            : tasks.FirstOrDefault(item => string.Equals(item.TaskId, taskId, StringComparison.Ordinal));

        if (task is not null)
        {
            var currentPages = string.IsNullOrEmpty(flowId) ? task.Pages ?? [] : FlowPagesOf(task, flowId);
            var inContext = FieldOn(currentPages, fieldId) ?? FieldOn(PagesOf(task), fieldId);
            if (inContext is not null)
                return inContext;
        }

        return FieldOn(tasks.SelectMany(PagesOf), fieldId);
    }

    private static Field? FieldOn(IEnumerable<Page> pages, string fieldId) =>
        pages
            .SelectMany(page => page.Fields ?? [])
            .FirstOrDefault(field => string.Equals(field.FieldId, fieldId, StringComparison.Ordinal));

    private static IEnumerable<Page> FlowPagesOf(Domain.Models.Task task, string flowId) =>
        (task.Summary?.Flows?.Where(flow => flow.FlowId == flowId).SelectMany(flow => flow.Pages ?? []) ?? [])
            .Concat(task.Summary?.DerivedFlows?.Where(flow => flow.FlowId == flowId).SelectMany(flow => flow.Pages ?? []) ?? []);

    private static IEnumerable<Page> PagesOf(Domain.Models.Task task) =>
        (task.Pages ?? [])
            .Concat(task.Summary?.Flows?.SelectMany(flow => flow.Pages ?? []) ?? [])
            .Concat(task.Summary?.DerivedFlows?.SelectMany(flow => flow.Pages ?? []) ?? []);
}
