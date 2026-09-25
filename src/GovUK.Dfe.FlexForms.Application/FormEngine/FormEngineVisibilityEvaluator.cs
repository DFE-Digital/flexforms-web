using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.FormEngine;
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

    /// <summary>
    /// Finds the next page in <paramref name="orderedPages"/> that is shown by a conditional
    /// show-page rule whose trigger fields are answered on <paramref name="currentPage"/>.
    /// Used by <see cref="NavigationAfterSave.Branch"/> navigation.
    /// </summary>
    public string? GetNextPageRevealedByCurrentPageFields(
        Page currentPage,
        Dictionary<string, object> data,
        IReadOnlyList<Page> orderedPages)
    {
        var revealed = GetPageIdsRevealedByCurrentPageFields(currentPage, data);
        if (revealed.Count == 0)
            return null;

        var index = FormStepPolicy.IndexOfPage(orderedPages, currentPage.PageId);
        if (index == -1)
            return null;

        for (var i = index + 1; i < orderedPages.Count; i++)
        {
            if (revealed.Contains(orderedPages[i].PageId))
                return orderedPages[i].PageId;
        }

        return null;
    }

    public HashSet<string> GetPageIdsRevealedByCurrentPageFields(
        Page currentPage,
        Dictionary<string, object> data)
    {
        var revealed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (template?.ConditionalLogic == null || currentPage.Fields == null || currentPage.Fields.Count == 0)
            return revealed;

        var currentFieldIds = new HashSet<string>(
            currentPage.Fields.Select(f => f.FieldId),
            StringComparer.OrdinalIgnoreCase);

        foreach (var rule in template.ConditionalLogic.Where(r => r.Enabled))
        {
            var showPageIds = rule.AffectedElements
                .Where(e => e.ElementType == "page"
                    && string.Equals(e.Action, "show", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(e.ElementId))
                .Select(e => e.ElementId)
                .ToList();
            if (showPageIds.Count == 0)
                continue;

            var triggerFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectTriggerFields(rule.ConditionGroup, triggerFields);
            if (!triggerFields.Any(t => currentFieldIds.Contains(t)))
                continue;

            if (!EvaluateRuleConditions(rule, data))
                continue;

            foreach (var pageIdToShow in showPageIds)
                revealed.Add(pageIdToShow);
        }

        return revealed;
    }

    private static void CollectTriggerFields(ConditionGroup? group, HashSet<string> targets)
    {
        if (group?.Conditions == null)
            return;

        foreach (var condition in group.Conditions)
            CollectTriggerFields(condition, targets);
    }

    private static void CollectTriggerFields(Condition? condition, HashSet<string> targets)
    {
        if (condition == null)
            return;

        if (!string.IsNullOrEmpty(condition.TriggerField))
            targets.Add(condition.TriggerField);

        if (condition.Conditions == null)
            return;

        foreach (var nested in condition.Conditions)
            CollectTriggerFields(nested, targets);
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
            data.TryGetValue(condition.TriggerField, out var rawValue);
            var conditionValue = condition.Value?.ToString() ?? "";
            var conditionMet = condition.Operator.ToLowerInvariant() switch
            {
                "equals" => EvaluateEquals(rawValue, conditionValue),
                "not_equals" => !EvaluateEquals(rawValue, conditionValue),
                "contains" => EvaluateContains(rawValue, conditionValue),
                "not_contains" => !EvaluateContains(rawValue, conditionValue),
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

    private static bool EvaluateEquals(object? fieldValue, string conditionValue) =>
        CheckboxValueNormalizer.Normalize(fieldValue).Any(v =>
            v.Equals(conditionValue, StringComparison.OrdinalIgnoreCase));

    private static bool EvaluateContains(object? fieldValue, string conditionValue)
    {
        if (string.IsNullOrEmpty(conditionValue))
            return false;

        var normalizedValues = CheckboxValueNormalizer.Normalize(fieldValue);
        if (normalizedValues.Count == 0)
            return false;

        return normalizedValues.Any(v =>
            v.Equals(conditionValue, StringComparison.OrdinalIgnoreCase)
            || v.Contains(conditionValue, StringComparison.OrdinalIgnoreCase));
    }
}
