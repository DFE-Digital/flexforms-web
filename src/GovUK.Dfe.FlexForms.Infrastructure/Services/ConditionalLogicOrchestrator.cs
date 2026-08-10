using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services;

/// <summary>
/// Service for orchestrating conditional logic execution across the form engine
/// </summary>
public class ConditionalLogicOrchestrator(
    IConditionalLogicEngine conditionalLogicEngine,
    ILogger<ConditionalLogicOrchestrator> logger) : IConditionalLogicOrchestrator
{
    /// <summary>
    /// Per-request (scoped) cache of expensive template walks shared across many item evaluations.
    /// </summary>
    private readonly Dictionary<string, TemplateStructureCache> _templateStructureCache =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<FormConditionalState> ApplyConditionalLogicAsync(FormTemplate template,
        Dictionary<string, object> formData,
        ConditionalLogicContext? context = null)
    {
        var state = new FormConditionalState();

        try
        {
            
            if (template.ConditionalLogic == null || !template.ConditionalLogic.Any())
            {
                // No conditional logic defined, return default state
                InitializeDefaultState(template, state);
                return state;
            }


            // Evaluate all conditional logic rules
            var result = conditionalLogicEngine.EvaluateRules(template.ConditionalLogic, formData, context);
            state.EvaluationResult = result;


            // Initialize with default state
            InitializeDefaultState(template, state);

            // Apply actions from conditional logic
            await ApplyActionsAsync(result.Actions, state, template, formData);

            logger.LogDebug("Applied conditional logic for template '{TemplateId}', {RuleCount} rules evaluated",
                template.TemplateId, result.EvaluatedRules.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error applying conditional logic for template '{TemplateId}'", template.TemplateId);
        }

        return state;
    }

    public async Task<FormConditionalState> ApplyFieldVisibilityAsync(
        FormTemplate template,
        Dictionary<string, object> formData,
        IReadOnlyCollection<string>? fieldIds = null,
        ConditionalLogicContext? context = null)
    {
        var state = new FormConditionalState();

        try
        {
            var structure = GetOrCreateTemplateStructure(template);
            var targetFields = fieldIds == null || fieldIds.Count == 0
                ? structure.FieldsWithVisibilityRules
                : fieldIds
                    .Where(id => structure.FieldsWithVisibilityRules.Contains(id))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var fieldId in targetFields)
            {
                // Affected fields default to hidden until a matching show action fires.
                state.FieldVisibility[fieldId] = false;
            }

            if (template.ConditionalLogic == null || targetFields.Count == 0)
            {
                return state;
            }

            var relevantRules = template.ConditionalLogic.Where(rule =>
                rule.Enabled
                && rule.AffectedElements != null
                && rule.AffectedElements.Any(element =>
                    string.Equals(element.ElementType, ConditionalLogicConstants.ElementTypes.Field, StringComparison.OrdinalIgnoreCase)
                    && targetFields.Contains(element.ElementId)
                    && (string.Equals(element.Action, ConditionalLogicConstants.Actions.Show, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(element.Action, ConditionalLogicConstants.Actions.Hide, StringComparison.OrdinalIgnoreCase))));

            var result = conditionalLogicEngine.EvaluateRules(relevantRules, formData, context);
            state.EvaluationResult = result;

            foreach (var action in result.Actions.OrderBy(a => a.Priority))
            {
                var element = action.Element;
                if (!string.Equals(element.ElementType, ConditionalLogicConstants.ElementTypes.Field, StringComparison.OrdinalIgnoreCase)
                    || !targetFields.Contains(element.ElementId))
                {
                    continue;
                }

                if (string.Equals(element.Action, ConditionalLogicConstants.Actions.Show, StringComparison.OrdinalIgnoreCase))
                {
                    state.FieldVisibility[element.ElementId] = true;
                }
                else if (string.Equals(element.Action, ConditionalLogicConstants.Actions.Hide, StringComparison.OrdinalIgnoreCase))
                {
                    state.FieldVisibility[element.ElementId] = false;
                }
            }

            logger.LogDebug(
                "Applied field visibility for template '{TemplateId}', {FieldCount} fields, {RuleCount} rules",
                template.TemplateId, targetFields.Count, result.EvaluatedRules.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error applying field visibility for template '{TemplateId}'", template.TemplateId);
        }

        return await System.Threading.Tasks.Task.FromResult(state);
    }

    public async Task<ConditionalLogicResult> EvaluateFieldChangeAsync(FormTemplate template,
        Dictionary<string, object> formData,
        string changedFieldId,
        ConditionalLogicContext? context = null)
    {
        try
        {
            if (template.ConditionalLogic == null || !template.ConditionalLogic.Any())
            {
                return new ConditionalLogicResult();
            }

            // Get only rules that are triggered by this field
            var triggeredRules = conditionalLogicEngine.GetTriggeredRules(template.ConditionalLogic, changedFieldId);

            // Evaluate only the triggered rules
            var result = conditionalLogicEngine.EvaluateRules(triggeredRules, formData, context);

            logger.LogDebug("Evaluated field change for '{FieldId}', {RuleCount} rules triggered",
                changedFieldId, triggeredRules.Count());

            return await System.Threading.Tasks.Task.FromResult(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error evaluating field change for '{FieldId}'", changedFieldId);
            return new ConditionalLogicResult
            {
                Errors = { $"Error evaluating field change: {ex.Message}" }
            };
        }
    }

    public async Task<Dictionary<string, bool>> GetElementVisibilityAsync(FormTemplate template,
        Dictionary<string, object> formData,
        ConditionalLogicContext? context = null)
    {
        var visibility = new Dictionary<string, bool>();

        try
        {
            var state = await ApplyConditionalLogicAsync(template, formData, context);
            
            // Combine field and page visibility
            foreach (var kvp in state.FieldVisibility)
            {
                visibility[kvp.Key] = kvp.Value;
            }

            foreach (var kvp in state.PageVisibility)
            {
                visibility[kvp.Key] = kvp.Value;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting element visibility");
        }

        return visibility;
    }

    public async Task<Dictionary<string, bool>> GetFieldRequiredStateAsync(FormTemplate template,
        Dictionary<string, object> formData,
        ConditionalLogicContext? context = null)
    {
        try
        {
            var state = await ApplyConditionalLogicAsync(template, formData, context);
            return state.FieldRequired;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting field required state");
            return new Dictionary<string, bool>();
        }
    }

    public async Task<List<ConditionalLogicValidationResult>> ValidateTemplateRulesAsync(FormTemplate template)
    {
        var results = new List<ConditionalLogicValidationResult>();

        try
        {
            if (template.ConditionalLogic == null || !template.ConditionalLogic.Any())
            {
                return results;
            }

            foreach (var rule in template.ConditionalLogic)
            {
                var validationResult = conditionalLogicEngine.ValidateRule(rule);
                results.Add(validationResult);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating template rules for '{TemplateId}'", template.TemplateId);
            results.Add(new ConditionalLogicValidationResult
            {
                IsValid = false,
                Errors = { $"Validation error: {ex.Message}" }
            });
        }

        return await System.Threading.Tasks.Task.FromResult(results);
    }

    public async Task<string?> GetNextPageAsync(FormTemplate template,
        Dictionary<string, object> formData,
        string currentPageId,
        ConditionalLogicContext? context = null)
    {
        try
        {
            

            var state = await ApplyConditionalLogicAsync(template, formData, context);


            // Find the current page and get the next one in sequence
            var allPages = GetAllPages(template);
            var currentPageIndex = allPages.FindIndex(p => p.PageId == currentPageId);


            if (currentPageIndex == -1 || currentPageIndex >= allPages.Count - 1)
            {
                return null; // Last page or page not found
            }

            // Look for the next visible page
            for (int i = currentPageIndex + 1; i < allPages.Count; i++)
            {
                var nextPage = allPages[i];
                
                // Check if this page should be skipped
                if (state.SkippedPages.Contains(nextPage.PageId))
                {
                    continue;
                }

                // Check if this page is visible
                if (state.PageVisibility.TryGetValue(nextPage.PageId, out var isVisible) && !isVisible)
                {
                    continue;
                }

                // NEW: Check if all fields on the next page are hidden by conditional logic
                var nextPageFields = GetFieldsForPage(template, nextPage.PageId);
                
                if (nextPageFields.Any() && nextPageFields.All(f => state.FieldVisibility.TryGetValue(f.FieldId, out var fieldIsVisible) && !fieldIsVisible))
                {
                    continue;
                }

                return nextPage.PageId;
            }

            return null; // No more visible pages
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting next page for '{CurrentPageId}'", currentPageId);
            return null;
        }
    }

    public async Task<bool> ShouldSkipPageAsync(FormTemplate template,
        Dictionary<string, object> formData,
        string pageId,
        ConditionalLogicContext? context = null)
    {
        try
        {
            var state = await ApplyConditionalLogicAsync(template, formData, context);
            
            // Check if page is in skipped list
            if (state.SkippedPages.Contains(pageId))
            {
                return true;
            }

            // Check if page is hidden
            if (state.PageVisibility.TryGetValue(pageId, out var isVisible) && !isVisible)
            {
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking if page '{PageId}' should be skipped", pageId);
            return false;
        }
    }

    #region Private Helper Methods

    private void InitializeDefaultState(FormTemplate template, FormConditionalState state)
    {
        var structure = GetOrCreateTemplateStructure(template);

        foreach (var page in structure.AllPages)
        {
            var isAffected = structure.AffectedPages.Contains(page.PageId);
            state.PageVisibility[page.PageId] = isAffected ? false : true;
        }

        foreach (var field in structure.AllFields)
        {
            var isAffected = structure.AffectedFields.Contains(field.FieldId);
            state.FieldVisibility[field.FieldId] = isAffected ? false : true;
            state.FieldEnabled[field.FieldId] = true;
            state.FieldRequired[field.FieldId] = field.Required ?? false;
        }
    }

    private TemplateStructureCache GetOrCreateTemplateStructure(FormTemplate template)
    {
        var cacheKey = template.TemplateId ?? string.Empty;
        if (_templateStructureCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var allPages = BuildAllPages(template);
        var allFields = allPages
            .Where(p => p.Fields != null)
            .SelectMany(p => p.Fields)
            .ToList();

        var affectedPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var affectedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fieldsWithVisibilityRules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (template.ConditionalLogic != null)
        {
            foreach (var rule in template.ConditionalLogic.Where(r => r.Enabled))
            {
                if (rule?.AffectedElements == null) continue;
                foreach (var element in rule.AffectedElements)
                {
                    if (element == null) continue;
                    switch (element.ElementType?.ToLowerInvariant())
                    {
                        case ConditionalLogicConstants.ElementTypes.Page:
                            affectedPages.Add(element.ElementId);
                            break;
                        case ConditionalLogicConstants.ElementTypes.Field:
                            affectedFields.Add(element.ElementId);
                            if (string.Equals(element.Action, ConditionalLogicConstants.Actions.Show, StringComparison.OrdinalIgnoreCase)
                                || string.Equals(element.Action, ConditionalLogicConstants.Actions.Hide, StringComparison.OrdinalIgnoreCase))
                            {
                                fieldsWithVisibilityRules.Add(element.ElementId);
                            }
                            break;
                    }
                }
            }
        }

        cached = new TemplateStructureCache(allPages, allFields, affectedPages, affectedFields, fieldsWithVisibilityRules);
        _templateStructureCache[cacheKey] = cached;
        return cached;
    }

    private async System.Threading.Tasks.Task ApplyActionsAsync(List<ConditionalLogicAction> actions,
        FormConditionalState state,
        FormTemplate template,
        Dictionary<string, object> formData)
    {
        foreach (var action in actions.OrderBy(a => a.Priority))
        {
            try
            {
                await ApplyActionAsync(action, state, template, formData);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error applying action '{Action}' for element '{ElementId}' from rule '{RuleId}'",
                    action.Element.Action, action.Element.ElementId, action.RuleId);
            }
        }
    }

    private async System.Threading.Tasks.Task ApplyActionAsync(ConditionalLogicAction action,
        FormConditionalState state,
        FormTemplate template,
        Dictionary<string, object> formData)
    {
        var element = action.Element;

        switch (element.Action.ToLowerInvariant())
        {
            case ConditionalLogicConstants.Actions.Show:
                ApplyShowAction(element, state);
                break;

            case ConditionalLogicConstants.Actions.Hide:
                ApplyHideAction(element, state);
                break;

            case ConditionalLogicConstants.Actions.Skip:
                ApplySkipAction(element, state);
                break;

            case ConditionalLogicConstants.Actions.Require:
                ApplyRequireAction(element, state);
                break;

            case ConditionalLogicConstants.Actions.MakeOptional:
                ApplyMakeOptionalAction(element, state);
                break;

            case ConditionalLogicConstants.Actions.Enable:
                ApplyEnableAction(element, state);
                break;

            case ConditionalLogicConstants.Actions.Disable:
                ApplyDisableAction(element, state);
                break;

            case ConditionalLogicConstants.Actions.SetValue:
                ApplySetValueAction(element, state);
                break;

            case ConditionalLogicConstants.Actions.ClearValue:
                ApplyClearValueAction(element, state);
                break;

            case ConditionalLogicConstants.Actions.AddValidation:
                ApplyAddValidationAction(element, state);
                break;

            case ConditionalLogicConstants.Actions.RemoveValidation:
                ApplyRemoveValidationAction(element, state);
                break;

            case ConditionalLogicConstants.Actions.ShowMessage:
                ApplyShowMessageAction(element, state, action.RuleId);
                break;

            default:
                logger.LogWarning("Unknown action '{Action}' for element '{ElementId}'",
                    element.Action, element.ElementId);
                break;
        }

        await System.Threading.Tasks.Task.CompletedTask;
    }

    private void ApplyShowAction(AffectedElement element, FormConditionalState state)
    {
        switch (element.ElementType.ToLowerInvariant())
        {
            case ConditionalLogicConstants.ElementTypes.Field:
                state.FieldVisibility[element.ElementId] = true;
                break;
            case ConditionalLogicConstants.ElementTypes.Page:
                state.PageVisibility[element.ElementId] = true;
                break;
        }
    }

    private void ApplyHideAction(AffectedElement element, FormConditionalState state)
    {
        switch (element.ElementType.ToLowerInvariant())
        {
            case ConditionalLogicConstants.ElementTypes.Field:
                state.FieldVisibility[element.ElementId] = false;
                break;
            case ConditionalLogicConstants.ElementTypes.Page:
                state.PageVisibility[element.ElementId] = false;
                break;
        }
    }

    private void ApplySkipAction(AffectedElement element, FormConditionalState state)
    {
            
        if (element.ElementType.ToLowerInvariant() == ConditionalLogicConstants.ElementTypes.Page)
        {
            state.SkippedPages.Add(element.ElementId);
        }
    }

    private void ApplyRequireAction(AffectedElement element, FormConditionalState state)
    {
        if (element.ElementType.ToLowerInvariant() == ConditionalLogicConstants.ElementTypes.Field)
        {
            state.FieldRequired[element.ElementId] = true;
        }
    }

    private void ApplyMakeOptionalAction(AffectedElement element, FormConditionalState state)
    {
        if (element.ElementType.ToLowerInvariant() == ConditionalLogicConstants.ElementTypes.Field)
        {
            state.FieldRequired[element.ElementId] = false;
        }
    }

    private void ApplyEnableAction(AffectedElement element, FormConditionalState state)
    {
        if (element.ElementType.ToLowerInvariant() == ConditionalLogicConstants.ElementTypes.Field)
        {
            state.FieldEnabled[element.ElementId] = true;
        }
    }

    private void ApplyDisableAction(AffectedElement element, FormConditionalState state)
    {
        if (element.ElementType.ToLowerInvariant() == ConditionalLogicConstants.ElementTypes.Field)
        {
            state.FieldEnabled[element.ElementId] = false;
        }
    }

    private void ApplySetValueAction(AffectedElement element, FormConditionalState state)
    {
        if (element.ElementType.ToLowerInvariant() == ConditionalLogicConstants.ElementTypes.Field &&
            element.ActionConfig?.TryGetValue("value", out var value) == true)
        {
            state.FieldValues[element.ElementId] = value;
        }
    }

    private void ApplyClearValueAction(AffectedElement element, FormConditionalState state)
    {
        if (element.ElementType.ToLowerInvariant() == ConditionalLogicConstants.ElementTypes.Field)
        {
            state.FieldValues[element.ElementId] = string.Empty;
        }
    }

    private void ApplyAddValidationAction(AffectedElement element, FormConditionalState state)
    {
        if (element.ElementType.ToLowerInvariant() == ConditionalLogicConstants.ElementTypes.Field &&
            element.ActionConfig != null)
        {
            if (!state.AdditionalValidations.ContainsKey(element.ElementId))
            {
                state.AdditionalValidations[element.ElementId] = new List<ValidationRule>();
            }

            // Create validation rule from action config
            var validationRule = CreateValidationRuleFromConfig(element.ActionConfig);
            if (validationRule != null)
            {
                state.AdditionalValidations[element.ElementId].Add(validationRule);
            }
        }
    }

    private void ApplyRemoveValidationAction(AffectedElement element, FormConditionalState state)
    {
        if (element.ElementType.ToLowerInvariant() == ConditionalLogicConstants.ElementTypes.Field &&
            element.ActionConfig?.TryGetValue("validationType", out var validationType) == true)
        {
            if (state.AdditionalValidations.ContainsKey(element.ElementId))
            {
                state.AdditionalValidations[element.ElementId]
                    .RemoveAll(v => v.Type.Equals(validationType.ToString(), StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    private void ApplyShowMessageAction(AffectedElement element, FormConditionalState state, string ruleId)
    {
        if (element.ActionConfig?.TryGetValue("message", out var message) == true)
        {
            var messageType = element.ActionConfig.TryGetValue("messageType", out var type) 
                ? type.ToString() ?? "info" 
                : "info";

            state.Messages.Add(new ConditionalLogicMessage
            {
                Text = message.ToString() ?? string.Empty,
                Type = messageType,
                TargetElement = element.ElementId,
                RuleId = ruleId
            });
        }
    }

    private ValidationRule? CreateValidationRuleFromConfig(Dictionary<string, object> config)
    {
        if (!config.TryGetValue("validationType", out var validationType))
        {
            return null;
        }

        var rule = new ValidationRule
        {
            Type = validationType.ToString() ?? string.Empty,
            Message = config.TryGetValue("message", out var message) ? message.ToString() ?? string.Empty : string.Empty,
            Rule = config.TryGetValue("rule", out var ruleValue) ? ruleValue : string.Empty
        };

        return rule;
    }

    private List<Page> GetAllPages(FormTemplate template) =>
        GetOrCreateTemplateStructure(template).AllPages;

    private List<Field> GetFieldsForPage(FormTemplate template, string pageId)
    {
        var page = GetAllPages(template).FirstOrDefault(p => p.PageId == pageId);
        if (page?.Fields != null)
        {
            return page.Fields.ToList();
        }

        return new List<Field>();
    }

    private static List<Page> BuildAllPages(FormTemplate template)
    {
        var pages = new List<Page>();

        if (template.TaskGroups != null)
        {
            foreach (var group in template.TaskGroups)
            {
                if (group.Tasks != null)
                {
                    foreach (var task in group.Tasks)
                    {
                        if (task.Pages != null)
                        {
                            pages.AddRange(task.Pages);
                        }

                        if (task.Summary?.Flows != null)
                        {
                            foreach (var flow in task.Summary.Flows)
                            {
                                pages.AddRange(flow.Pages);
                            }
                        }

                        if (task.Summary?.DerivedFlows != null)
                        {
                            foreach (var flow in task.Summary.DerivedFlows)
                            {
                                pages.AddRange(flow.Pages);
                            }
                        }
                    }
                }
            }
        }

        return pages;
    }

    private sealed class TemplateStructureCache(
        List<Page> allPages,
        List<Field> allFields,
        HashSet<string> affectedPages,
        HashSet<string> affectedFields,
        HashSet<string> fieldsWithVisibilityRules)
    {
        public List<Page> AllPages { get; } = allPages;
        public List<Field> AllFields { get; } = allFields;
        public HashSet<string> AffectedPages { get; } = affectedPages;
        public HashSet<string> AffectedFields { get; } = affectedFields;
        public HashSet<string> FieldsWithVisibilityRules { get; } = fieldsWithVisibilityRules;
    }

    #endregion
}
