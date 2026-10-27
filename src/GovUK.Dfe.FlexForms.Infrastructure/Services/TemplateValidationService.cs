using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services;

/// <summary>
/// Service for validating form template JSON against domain models
/// </summary>
public class TemplateValidationService(ILogger<TemplateValidationService> logger) : ITemplateValidationService
{
    private readonly ILogger<TemplateValidationService> _logger = logger;

    /// <summary>
    /// Validates a JSON string against the FormTemplate domain model
    /// </summary>
    public (bool IsValid, List<string> Errors) ValidateTemplateJson(string jsonString)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(jsonString))
        {
            errors.Add("JSON content is required");
            return (false, errors);
        }

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = false, // Enforce exact property name matching
                AllowTrailingCommas = true
            };

            // Attempt to deserialize to FormTemplate
            var template = JsonSerializer.Deserialize<FormTemplate>(jsonString, options);

            if (template == null)
            {
                errors.Add("Failed to parse JSON as a valid template");
                return (false, errors);
            }

            // Validate required fields
            ValidateTemplate(template, errors);

            return (errors.Count == 0, errors);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "JSON deserialization error during template validation");
            errors.Add($"JSON parsing error: {ex.Message}");
            
            // Try to provide more helpful error messages
            if (ex.Message.Contains("property", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("This may be caused by a misspelled property name or incorrect JSON structure");
            }
            
            return (false, errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during template validation");
            errors.Add($"Validation error: {ex.Message}");
            return (false, errors);
        }
    }

    /// <summary>
    /// Attempts to parse and validate a JSON string as a FormTemplate
    /// </summary>
    public (FormTemplate? Template, List<string> Errors) TryParseTemplate(string jsonString)
    {
        var (isValid, errors) = ValidateTemplateJson(jsonString);
        
        if (!isValid)
        {
            return (null, errors);
        }

        try
        {
            var template = JsonSerializer.Deserialize<FormTemplate>(jsonString);
            return (template, errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse template after validation");
            errors.Add($"Failed to parse template: {ex.Message}");
            return (null, errors);
        }
    }

    /// <summary>
    /// Validates the template structure and required fields
    /// </summary>
    private void ValidateTemplate(FormTemplate template, List<string> errors)
    {
        // Validate required top-level fields
        if (string.IsNullOrWhiteSpace(template.TemplateId))
        {
            errors.Add("Template must have a 'templateId'");
        }

        if (string.IsNullOrWhiteSpace(template.TemplateName))
        {
            errors.Add("Template must have a 'templateName'");
        }

        // Validate task groups
        if (template.TaskGroups == null || template.TaskGroups.Count == 0)
        {
            errors.Add("Template must have at least one task group in 'taskGroups'");
            return;
        }

        for (int i = 0; i < template.TaskGroups.Count; i++)
        {
            var group = template.TaskGroups[i];
            ValidateTaskGroup(group, i, errors);
        }

        ValidateApplicationMetaData(template, errors);
        ValidateStructure(template, errors);
    }

    /// <summary>
    /// Field types that <c>FieldRendererService</c> can render.
    /// </summary>
    public static readonly IReadOnlySet<string> SupportedFieldTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "text", "email", "text-area", "character-count", "radios", "checkboxes", "select", "date", "complexField"
    };

    private static readonly HashSet<string> ChoiceFieldTypes = new(StringComparer.Ordinal) { "radios", "checkboxes", "select" };

    /// <summary>
    /// Catches problems that only show up when the form is used: duplicate ids, field types that can't be rendered,
    /// choice questions without options, and conditional logic that points at nothing.
    /// </summary>
    private static void ValidateStructure(FormTemplate template, List<string> errors)
    {
        AddDuplicateErrors(template.TaskGroups.Select(group => group.GroupId), "groupId", "the template", errors);

        var tasks = template.TaskGroups.SelectMany(group => group.Tasks ?? []).ToList();
        AddDuplicateErrors(tasks.Select(task => task.TaskId), "taskId", "the template", errors);

        // Answers to task pages, and each collection as a whole, are stored by fieldId at the top level of the application.
        var topLevelFieldIds = new List<string>();
        var knownFieldIds = new HashSet<string>(StringComparer.Ordinal);
        var knownPageIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var task in tasks)
        {
            if (task.Pages is { Count: > 0 } pages)
            {
                ValidatePageSet(pages, $"task '{task.TaskId}'", errors, knownFieldIds, knownPageIds);
                topLevelFieldIds.AddRange(pages.SelectMany(page => page.Fields ?? []).Select(field => field.FieldId));
            }

            foreach (var flow in task.Summary?.Flows ?? [])
            {
                topLevelFieldIds.Add(flow.FieldId);
                knownFieldIds.Add(flow.FieldId);
                ValidatePageSet(flow.Pages ?? [], $"collection '{flow.FlowId}' in task '{task.TaskId}'", errors, knownFieldIds, knownPageIds);
                AddDuplicateErrors(
                    (flow.Pages ?? []).SelectMany(page => page.Fields ?? []).Select(field => field.FieldId),
                    "fieldId",
                    $"collection '{flow.FlowId}'",
                    errors);
            }

            foreach (var flow in task.Summary?.DerivedFlows ?? [])
            {
                topLevelFieldIds.Add(flow.FieldId);
                knownFieldIds.Add(flow.FieldId);
                ValidatePageSet(flow.Pages ?? [], $"derived list '{flow.FlowId}' in task '{task.TaskId}'", errors, knownFieldIds, knownPageIds);
                AddDuplicateErrors(
                    (flow.Pages ?? []).SelectMany(page => page.Fields ?? []).Select(field => field.FieldId),
                    "fieldId",
                    $"derived list '{flow.FlowId}'",
                    errors);
            }
        }

        AddDuplicateErrors(topLevelFieldIds, "fieldId", "task pages and collections", errors);

        foreach (var rule in template.ConditionalLogic ?? [])
        {
            var ruleContext = $"Conditional logic rule '{(string.IsNullOrWhiteSpace(rule.Id) ? rule.Name : rule.Id)}'";

            foreach (var trigger in TriggerFields(rule.ConditionGroup?.Conditions))
            {
                if (!knownFieldIds.Contains(trigger))
                    errors.Add($"{ruleContext}: triggerField '{trigger}' is not a field in this template");
            }

            foreach (var element in rule.AffectedElements ?? [])
            {
                var known = element.ElementType switch
                {
                    "field" => knownFieldIds,
                    "page" => knownPageIds,
                    _ => null
                };

                if (known is not null && !string.IsNullOrWhiteSpace(element.ElementId) && !known.Contains(element.ElementId))
                    errors.Add($"{ruleContext}: {element.ElementType} '{element.ElementId}' is not in this template");
            }
        }
    }

    private static void ValidatePageSet(
        IReadOnlyList<Page> pages,
        string context,
        List<string> errors,
        HashSet<string> knownFieldIds,
        HashSet<string> knownPageIds)
    {
        AddDuplicateErrors(pages.Select(page => page.PageId), "pageId", context, errors);

        foreach (var page in pages)
        {
            if (!string.IsNullOrWhiteSpace(page.PageId))
                knownPageIds.Add(page.PageId);

            foreach (var field in page.Fields ?? [])
            {
                if (!string.IsNullOrWhiteSpace(field.FieldId))
                    knownFieldIds.Add(field.FieldId);

                var fieldContext = $"Field '{field.FieldId}' on page '{page.PageId}' in {context}";

                if (string.IsNullOrWhiteSpace(field.Type))
                    continue;

                if (field.Type == "autocomplete")
                {
                    errors.Add($"{fieldContext}: type 'autocomplete' can't be shown to users. Use 'complexField' with a search complex field instead");
                }
                else if (!SupportedFieldTypes.Contains(field.Type))
                {
                    errors.Add($"{fieldContext}: type '{field.Type}' isn't supported. Use one of: {string.Join(", ", SupportedFieldTypes)}");
                }

                if (ChoiceFieldTypes.Contains(field.Type))
                {
                    if (field.Options is not { Count: > 0 })
                        errors.Add($"{fieldContext}: a '{field.Type}' question needs at least one option");
                    else if (field.Options.Any(option => string.IsNullOrWhiteSpace(option.Value)))
                        errors.Add($"{fieldContext}: every option needs a 'value'");
                }

                if (field.Type == "complexField" && string.IsNullOrWhiteSpace(field.ComplexField?.Id))
                    errors.Add($"{fieldContext}: 'complexField.id' is required");
            }
        }
    }

    private static IEnumerable<string> TriggerFields(IEnumerable<Condition>? conditions)
    {
        foreach (var condition in conditions ?? [])
        {
            if (!string.IsNullOrWhiteSpace(condition.TriggerField))
                yield return condition.TriggerField;

            foreach (var nested in TriggerFields(condition.Conditions))
                yield return nested;
        }
    }

    private static void AddDuplicateErrors(IEnumerable<string?> ids, string idName, string scope, List<string> errors)
    {
        var duplicates = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .GroupBy(id => id!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        foreach (var duplicate in duplicates)
            errors.Add($"{idName} '{duplicate}' is used more than once in {scope}. Each {idName} must be unique");
    }

    private static void ValidateApplicationMetaData(FormTemplate template, List<string> errors)
    {
        var configuration = template.ApplicationMetaData;
        if (configuration is null || configuration.Items is null || configuration.Items.Count == 0)
            return;

        if (configuration.Items.Count > ApplicationMetadataConfiguration.MaxItems)
        {
            errors.Add(
                $"applicationMetaData.items cannot contain more than {ApplicationMetadataConfiguration.MaxItems} entries");
        }

        for (var i = 0; i < configuration.Items.Count; i++)
        {
            var item = configuration.Items[i];
            var context = $"applicationMetaData.items[{i}]";

            if (string.IsNullOrWhiteSpace(item.Label))
                errors.Add($"{context}: 'label' is required");

            if (string.IsNullOrWhiteSpace(item.FieldId))
            {
                errors.Add($"{context}: 'fieldId' is required");
                continue;
            }

            if (ApplicationMetadataBindingValidator.Validate(item.FieldId, template) is { } problem)
            {
                var label = string.IsNullOrWhiteSpace(item.Label) ? string.Empty : $" ('{item.Label.Trim()}')";
                errors.Add($"{context}{label}: fieldId '{item.FieldId.Trim()}' is invalid: {problem}");
            }
        }
    }

    /// <summary>
    /// Validates a task group
    /// </summary>
    private void ValidateTaskGroup(TaskGroup group, int index, List<string> errors)
    {
        var groupContext = $"Task Group {index + 1}";

        if (string.IsNullOrWhiteSpace(group.GroupId))
        {
            errors.Add($"{groupContext}: 'groupId' is required");
        }

        if (string.IsNullOrWhiteSpace(group.GroupName))
        {
            errors.Add($"{groupContext}: 'groupName' is required");
        }

        if (group.Tasks == null || group.Tasks.Count == 0)
        {
            errors.Add($"{groupContext} ('{group.GroupName}'): must have at least one task");
            return;
        }

        for (int i = 0; i < group.Tasks.Count; i++)
        {
            var task = group.Tasks[i];
            ValidateTask(task, i, group.GroupName, errors);
        }
    }

    /// <summary>
    /// Validates a task
    /// </summary>
    private void ValidateTask(Domain.Models.Task task, int index, string groupName, List<string> errors)
    {
        var taskContext = $"Task '{task.TaskName ?? $"#{index + 1}"}' in group '{groupName}'";

        if (string.IsNullOrWhiteSpace(task.TaskId))
        {
            errors.Add($"{taskContext}: 'taskId' is required");
        }

        if (string.IsNullOrWhiteSpace(task.TaskName))
        {
            errors.Add($"{taskContext}: 'taskName' is required");
        }

        // Validate pages if present
        if (task.Pages != null && task.Pages.Count > 0)
        {
            for (int i = 0; i < task.Pages.Count; i++)
            {
                var page = task.Pages[i];
                ValidatePage(page, i, taskContext, errors);
            }
        }
    }

    /// <summary>
    /// Validates a page
    /// </summary>
    private void ValidatePage(Page page, int index, string taskContext, List<string> errors)
    {
        var pageContext = $"Page '{page.Title ?? $"#{index + 1}"}' in {taskContext}";

        if (string.IsNullOrWhiteSpace(page.PageId))
        {
            errors.Add($"{pageContext}: 'pageId' is required");
        }

        if (string.IsNullOrWhiteSpace(page.Title))
        {
            errors.Add($"{pageContext}: 'title' is required");
        }

        // Validate fields if present
        if (page.Fields != null && page.Fields.Count > 0)
        {
            for (int i = 0; i < page.Fields.Count; i++)
            {
                var field = page.Fields[i];
                ValidateField(field, i, pageContext, errors);
            }
        }
    }

    /// <summary>
    /// Validates a field
    /// </summary>
    private void ValidateField(Field field, int index, string pageContext, List<string> errors)
    {
        var fieldContext = $"Field '{field.FieldId ?? $"#{index + 1}"}' in {pageContext}";

        if (string.IsNullOrWhiteSpace(field.FieldId))
        {
            errors.Add($"{fieldContext}: 'fieldId' is required");
        }

        if (string.IsNullOrWhiteSpace(field.Type))
        {
            errors.Add($"{fieldContext}: 'type' is required");
        }

        if (field.Label == null)
        {
            errors.Add($"{fieldContext}: 'label' object is required");
        }
        else if (string.IsNullOrWhiteSpace(field.Label.Value))
        {
            errors.Add($"{fieldContext}: 'label.value' is required");
        }

        // Validate validations if present
        if (field.Validations != null && field.Validations.Count > 0)
        {
            for (int i = 0; i < field.Validations.Count; i++)
            {
                var validation = field.Validations[i];
                ValidateValidationRule(validation, i, fieldContext, errors);
            }
        }
    }

    /// <summary>
    /// Validates a validation rule
    /// </summary>
    private void ValidateValidationRule(ValidationRule rule, int index, string fieldContext, List<string> errors)
    {
        var ruleContext = $"Validation rule #{index + 1} in {fieldContext}";

        if (string.IsNullOrWhiteSpace(rule.Type))
        {
            errors.Add($"{ruleContext}: 'type' is required");
        }

        if (string.IsNullOrWhiteSpace(rule.Message))
        {
            errors.Add($"{ruleContext}: 'message' is required");
        }
    }
}

