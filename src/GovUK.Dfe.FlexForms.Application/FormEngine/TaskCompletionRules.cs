using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.FormEngine;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.Extensions.Logging;
using TaskModel = GovUK.Dfe.FlexForms.Domain.Models.Task;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

/// <summary>
/// The single definition of when a task can be marked complete. Shared by manual
/// completion and automatic completion.
/// </summary>
internal static class TaskCompletionRules
{
    public static List<string> CollectStandardAndCollectionErrors(
        TaskModel task,
        FormTemplate template,
        Dictionary<string, object> formData,
        IFieldRequirementService fieldRequirementService,
        FormEngineVisibilityEvaluator visibility,
        ILogger logger)
    {
        var errorLines = new List<string>();
        var missingFieldsWithMessages = fieldRequirementService.GetMissingRequiredFieldsWithMessages(
            task,
            template,
            formData,
            visibility.IsFieldHidden);

        errorLines.AddRange(missingFieldsWithMessages.Values);

        if (task.Summary?.Mode?.Equals("multiCollectionFlow", StringComparison.OrdinalIgnoreCase) != true
            || task.Summary.Flows == null
            || !task.Summary.Flows.Any())
        {
            return errorLines;
        }

        foreach (var flow in task.Summary.Flows)
        {
            var items = FormEngineCollectionItems.Read(formData, flow.FieldId);
            var requiredMin = flow.MinItems ?? 1;
            if (items.Count < requiredMin)
            {
                var flowTitle = string.IsNullOrWhiteSpace(flow.Title)
                    ? (string.IsNullOrWhiteSpace(task.TaskName) ? "this section" : task.TaskName)
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
                    .Where(f => fieldRequirementService.IsFieldRequired(f, template))
                    .Select(f => f.FieldId)
                    .ToList();
                visibility.EnsureItemFieldVisibility(item, requiredFieldIds);

                var itemHasMissingFields = flow.Pages
                    .Where(page => page?.Fields != null)
                    .SelectMany(page => page.Fields)
                    .Where(field => fieldRequirementService.IsFieldRequired(field, template))
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
                    ? (string.IsNullOrWhiteSpace(task.TaskName) ? "this section" : task.TaskName)
                    : flow.Title;
                errorLines.Add($"Complete all required questions for each item in {flowTitle}");
                logger.LogInformation("Collection flow '{FlowId}' has an item with incomplete required fields", flow.FlowId);
                break;
            }
        }

        return errorLines;
    }

    public static List<string> CollectDerivedErrors(
        TaskModel? task,
        Dictionary<string, object> formData,
        IDerivedCollectionFlowService derivedCollectionFlowService)
    {
        var errorLines = new List<string>();
        var derivedFlows = task?.Summary?.DerivedFlows;
        if (derivedFlows == null || derivedFlows.Count == 0)
            return errorLines;

        foreach (var derivedFlow in derivedFlows)
        {
            var derivedItems = derivedCollectionFlowService.GenerateItemsFromSourceField(
                derivedFlow.SourceFieldId,
                formData,
                derivedFlow);

            if (derivedItems.Count == 0)
            {
                var errorMessage = !string.IsNullOrEmpty(derivedFlow.NoItemsErrorMessage)
                    ? derivedFlow.NoItemsErrorMessage
                    : $"You need to add at least one item before signing the {derivedFlow.Title}";
                errorLines.Add(errorMessage);
                continue;
            }

            var statuses = derivedCollectionFlowService.GetItemStatuses(derivedFlow.FieldId, formData);
            var unsignedItems = derivedItems
                .Where(item => !statuses.ContainsKey(item.Id) || statuses[item.Id] != "Signed")
                .ToList();

            foreach (var item in unsignedItems)
            {
                var displayName = FormEngineDerivedItems.GetDisplayName(
                    derivedFlow,
                    item.Id,
                    formData,
                    derivedCollectionFlowService);
                var errorMessage = !string.IsNullOrEmpty(derivedFlow.UnsignedItemErrorMessage)
                    ? derivedFlow.UnsignedItemErrorMessage.Replace("{sourceName}", displayName)
                    : $"You need to sign the declaration for {displayName}";
                errorLines.Add(errorMessage);
            }
        }

        return errorLines;
    }

    /// <summary>
    /// True when automatic completion has something to judge: a visible required field,
    /// a multi-collection flow, or a derived flow.
    /// </summary>
    public static bool HasCompletionCriteria(
        TaskModel task,
        FormTemplate template,
        IFieldRequirementService fieldRequirementService)
    {
        if (task.Summary?.Flows?.Count > 0 || task.Summary?.DerivedFlows?.Count > 0)
            return true;

        return task.Pages?
            .Where(p => p?.Fields != null)
            .SelectMany(p => p.Fields)
            .Any(f => fieldRequirementService.IsFieldRequired(f, template)) == true;
    }
}
