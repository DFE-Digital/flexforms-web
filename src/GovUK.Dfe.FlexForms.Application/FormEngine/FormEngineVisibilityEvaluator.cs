using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

/// <summary>
/// Field/page visibility against a template and the current <see cref="FormConditionalState"/>.
/// Single place for the rules previously embedded in the PageModel.
/// </summary>
public sealed class FormEngineVisibilityEvaluator(
    FormTemplate? template,
    FormConditionalState? conditionalState,
    IConditionalLogicOrchestrator conditionalLogicOrchestrator,
    string pageId,
    string taskId,
    ILogger logger)
{
    private HashSet<string>? _fieldsWithConditionalVisibility;
    private readonly Dictionary<object, FormConditionalState> _itemConditionalStateCache =
        new(ReferenceEqualityComparer.Instance);

    public bool IsFieldHidden(string fieldId)
    {
        if (conditionalState == null)
        {
            if (template?.ConditionalLogic != null && HasFieldConditionalLogic(fieldId))
                return true;
            return false;
        }

        if (conditionalState.FieldVisibility.TryGetValue(fieldId, out var isVisible))
            return !isVisible;

        if (template?.ConditionalLogic != null && HasFieldConditionalLogic(fieldId))
            return true;

        return false;
    }

    public bool IsPageHidden(string pageIdToCheck)
    {
        if (conditionalState == null)
        {
            if (template?.ConditionalLogic != null && HasPageConditionalLogic(pageIdToCheck))
                return true;
            return false;
        }

        if (conditionalState.SkippedPages.Contains(pageIdToCheck))
            return true;

        if (conditionalState.PageVisibility.TryGetValue(pageIdToCheck, out var isVisible))
            return !isVisible;

        if (template?.ConditionalLogic != null && HasPageConditionalLogic(pageIdToCheck))
            return true;

        return false;
    }

    public bool HasConditionalLogicShowingPages(Dictionary<string, object> data)
    {
        if (template?.ConditionalLogic == null)
            return false;

        foreach (var rule in template.ConditionalLogic.Where(r => r.Enabled))
        {
            var hasShowPageAction = rule.AffectedElements.Any(element =>
                element.ElementType == "page" && element.Action == "show");
            if (!hasShowPageAction)
                continue;

            if (EvaluateRuleConditions(rule, data))
                return true;
        }

        return false;
    }

    public void EnsureItemFieldVisibility(Dictionary<string, object> itemData, IEnumerable<string> fieldIds)
    {
        if (template?.ConditionalLogic == null || !template.ConditionalLogic.Any())
            return;

        var needed = fieldIds
            .Where(HasFieldConditionalLogic)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(id =>
                !_itemConditionalStateCache.TryGetValue(itemData, out var existing)
                || !existing.FieldVisibility.ContainsKey(id))
            .ToList();

        if (needed.Count == 0)
            return;

        try
        {
            var context = new ConditionalLogicContext
            {
                CurrentPageId = pageId,
                CurrentTaskId = taskId,
                IsClientSide = false,
                Trigger = "load"
            };

            var partial = conditionalLogicOrchestrator
                .ApplyFieldVisibilityAsync(template, itemData, needed, context)
                .GetAwaiter()
                .GetResult();

            if (!_itemConditionalStateCache.TryGetValue(itemData, out var state))
            {
                _itemConditionalStateCache[itemData] = partial;
                return;
            }

            foreach (var kvp in partial.FieldVisibility)
                state.FieldVisibility[kvp.Key] = kvp.Value;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error ensuring field visibility for collection item");
        }
    }

    public bool IsFieldHiddenForItem(string fieldId, Dictionary<string, object> itemData)
    {
        try
        {
            if (template?.ConditionalLogic == null || !template.ConditionalLogic.Any())
                return false;

            if (!HasFieldConditionalLogic(fieldId))
                return false;

            EnsureItemFieldVisibility(itemData, [fieldId]);

            if (_itemConditionalStateCache.TryGetValue(itemData, out var itemConditionalState)
                && itemConditionalState.FieldVisibility.TryGetValue(fieldId, out var isVisible))
            {
                return !isVisible;
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking field visibility for collection item, field {FieldId}", fieldId);
            return false;
        }
    }

    public bool HasFieldConditionalLogic(string fieldId)
    {
        if (template?.ConditionalLogic == null)
            return false;

        _fieldsWithConditionalVisibility ??= BuildFieldsWithConditionalVisibility();
        return _fieldsWithConditionalVisibility.Contains(fieldId);
    }

    private HashSet<string> BuildFieldsWithConditionalVisibility()
    {
        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (template?.ConditionalLogic == null)
            return fields;

        foreach (var rule in template.ConditionalLogic)
        {
            if (!rule.Enabled || rule.AffectedElements == null)
                continue;

            foreach (var element in rule.AffectedElements)
            {
                if (element.ElementType == "field"
                    && (element.Action == "hide" || element.Action == "show")
                    && !string.IsNullOrEmpty(element.ElementId))
                {
                    fields.Add(element.ElementId);
                }
            }
        }

        return fields;
    }

    private bool HasPageConditionalLogic(string pageIdToCheck)
    {
        if (template?.ConditionalLogic == null)
            return false;

        return template.ConditionalLogic.Any(rule =>
            rule.Enabled
            && rule.AffectedElements.Any(e =>
                e.ElementType == "page"
                && (e.Action == "hide" || e.Action == "show")
                && e.ElementId == pageIdToCheck));
    }

    private static bool EvaluateRuleConditions(ConditionalLogic rule, Dictionary<string, object> data)
    {
        if (rule.ConditionGroup?.Conditions == null || !rule.ConditionGroup.Conditions.Any())
            return false;

        var results = new List<bool>();
        foreach (var condition in rule.ConditionGroup.Conditions)
        {
            var fieldValue = data.TryGetValue(condition.TriggerField, out var value) ? value?.ToString() : "";
            var conditionValue = condition.Value?.ToString() ?? "";
            var conditionMet = condition.Operator.ToLowerInvariant() switch
            {
                "equals" => string.Equals(fieldValue, conditionValue, StringComparison.OrdinalIgnoreCase),
                "not_equals" => !string.Equals(fieldValue, conditionValue, StringComparison.OrdinalIgnoreCase),
                "contains" => fieldValue?.Contains(conditionValue, StringComparison.OrdinalIgnoreCase) == true,
                "not_contains" => fieldValue?.Contains(conditionValue, StringComparison.OrdinalIgnoreCase) != true,
                _ => false
            };
            results.Add(conditionMet);
        }

        return rule.ConditionGroup.LogicalOperator?.ToUpperInvariant() switch
        {
            "AND" => results.All(r => r),
            "OR" => results.Any(r => r),
            _ => results.All(r => r)
        };
    }
}
