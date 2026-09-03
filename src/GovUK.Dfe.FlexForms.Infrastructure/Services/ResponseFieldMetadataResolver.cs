using System.Globalization;
using System.Text.Json;
using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services;

/// <summary>
/// Resolves question text and dataType for response JSON field entries from the template,
/// with runtime value fallback for dataType when the template type is unavailable.
/// Collection flows keep their existing raw <c>value</c> shape and get an additive <c>fields</c> map.
/// </summary>
public static class ResponseFieldMetadataResolver
{
    public sealed record FieldMetadata(string Question, string? TemplateFieldType, string? TaskId = null);

    public sealed class TemplateFieldLookup
    {
        public Dictionary<string, FieldMetadata> Fields { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Collection/flow storage fieldId → nested template fields (question + type only).
        /// </summary>
        public Dictionary<string, Dictionary<string, FieldMetadata>> CollectionNestedFields { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> TaskIds { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public static TemplateFieldLookup BuildLookup(FormTemplate? template)
    {
        var lookup = new TemplateFieldLookup();
        if (template?.TaskGroups == null)
        {
            return lookup;
        }

        foreach (var task in template.TaskGroups.SelectMany(g => g.Tasks))
        {
            if (!string.IsNullOrWhiteSpace(task.TaskId))
            {
                lookup.TaskIds.Add(task.TaskId);
            }

            AddPages(lookup.Fields, task.Pages, task.TaskId);

            if (task.Summary?.Flows != null)
            {
                foreach (var flow in task.Summary.Flows)
                {
                    AddCollectionFlow(lookup, flow.FieldId, flow.Title, flow.FlowId, flow.Pages, task.TaskId);
                }
            }

            if (task.Summary?.DerivedFlows != null)
            {
                foreach (var flow in task.Summary.DerivedFlows)
                {
                    AddCollectionFlow(lookup, flow.FieldId, flow.Title, flow.FlowId, flow.Pages, task.TaskId);
                }
            }
        }

        return lookup;
    }

    public static string ResolveQuestion(string fieldId, TemplateFieldLookup lookup)
    {
        return lookup.Fields.TryGetValue(fieldId, out var metadata)
            ? metadata.Question
            : string.Empty;
    }

    public static string ResolveDataType(
        string fieldId,
        string? value,
        TemplateFieldLookup lookup)
    {
        if (lookup.CollectionNestedFields.ContainsKey(fieldId))
        {
            return "array";
        }

        if (lookup.Fields.TryGetValue(fieldId, out var metadata) &&
            !string.IsNullOrWhiteSpace(metadata.TemplateFieldType))
        {
            var mapped = MapTemplateTypeToDataType(metadata.TemplateFieldType);
            if (!string.IsNullOrWhiteSpace(mapped))
            {
                return mapped;
            }
        }

        return InferDataTypeFromValue(value);
    }

    /// <summary>
    /// True only when the field's parent task was explicitly marked complete
    /// (the "Mark this section as complete" checkbox), not merely because the field has a value.
    /// </summary>
    public static bool ResolveCompleted(
        string fieldId,
        TemplateFieldLookup lookup,
        IReadOnlyDictionary<string, string> taskStatusData)
    {
        if (TryResolveTaskId(fieldId, lookup, out var taskId)
            && IsTaskMarkedCompleted(taskId, taskStatusData))
        {
            return true;
        }

        const string completedSuffix = "_completed";
        if (fieldId.EndsWith(completedSuffix, StringComparison.OrdinalIgnoreCase))
        {
            var candidate = fieldId[..^completedSuffix.Length];
            if (IsTaskMarkedCompleted(candidate, taskStatusData))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsTaskMarkedCompleted(
        string? taskId,
        IReadOnlyDictionary<string, string> taskStatusData)
    {
        if (string.IsNullOrWhiteSpace(taskId) || taskStatusData.Count == 0)
        {
            return false;
        }

        return taskStatusData.TryGetValue(taskId, out var status)
               && string.Equals(status, nameof(Domain.Models.TaskStatus.Completed), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds a response entry. Always keeps <paramref name="value"/> unchanged.
    /// For collection flows, adds an additive <c>fields</c> map of nested question/dataType metadata.
    /// <paramref name="completed"/> defaults to false; callers should pass true only when the
    /// parent task was explicitly marked complete.
    /// </summary>
    public static object BuildFormFieldEntry(
        string fieldId,
        string value,
        TemplateFieldLookup lookup,
        bool completed = false)
    {
        var question = ResolveQuestion(fieldId, lookup);
        var dataType = ResolveDataType(fieldId, value, lookup);

        if (lookup.CollectionNestedFields.TryGetValue(fieldId, out var nestedFields) &&
            nestedFields.Count > 0)
        {
            var fields = nestedFields.ToDictionary(
                kvp => kvp.Key,
                kvp => new
                {
                    question = kvp.Value.Question,
                    dataType = ResolveNestedDataType(kvp.Value)
                },
                StringComparer.OrdinalIgnoreCase);

            return new
            {
                question,
                value,
                completed,
                dataType,
                fields
            };
        }

        return new
        {
            question,
            value,
            completed,
            dataType
        };
    }

    public static string MapTemplateTypeToDataType(string fieldType)
    {
        return fieldType.Trim().ToLowerInvariant() switch
        {
            "date" or "datetime" or "date-time" => "DateTime",
            "text" or "textarea" or "text-area" or "character-count"
                or "email" or "select" or "radios" or "checkboxes"
                or "autocomplete" or "complexfield" or "complex-field" => "string",
            "number" or "numeric" or "integer" or "decimal" => "number",
            "boolean" or "bool" => "boolean",
            _ => string.Empty
        };
    }

    public static string InferDataTypeFromValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "string";
        }

        var trimmed = value.Trim();

        if (bool.TryParse(trimmed, out _))
        {
            return "boolean";
        }

        if (TryParseDateTime(trimmed))
        {
            return "DateTime";
        }

        if (decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
        {
            return "number";
        }

        if (trimmed.StartsWith('[') || trimmed.StartsWith('{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                return doc.RootElement.ValueKind switch
                {
                    JsonValueKind.Array => "array",
                    JsonValueKind.Object => "object",
                    _ => "string"
                };
            }
            catch (JsonException)
            {
                // fall through
            }
        }

        return "string";
    }

    private static string ResolveNestedDataType(FieldMetadata metadata)
    {
        if (!string.IsNullOrWhiteSpace(metadata.TemplateFieldType))
        {
            var mapped = MapTemplateTypeToDataType(metadata.TemplateFieldType);
            if (!string.IsNullOrWhiteSpace(mapped))
            {
                return mapped;
            }
        }

        return "string";
    }

    private static bool TryParseDateTime(string value)
    {
        string[] exactFormats =
        [
            "yyyy-MM-dd",
            "dd/MM/yyyy",
            "d/M/yyyy",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ssZ",
            "yyyy-MM-ddTHH:mm:ss.fffZ"
        ];

        return DateTime.TryParseExact(
                   value,
                   exactFormats,
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                   out _)
               || DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _)
               || DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out _);
    }

    internal static bool TryResolveTaskId(string fieldId, TemplateFieldLookup lookup, out string taskId)
    {
        if (lookup.Fields.TryGetValue(fieldId, out var metadata)
            && !string.IsNullOrWhiteSpace(metadata.TaskId))
        {
            taskId = metadata.TaskId;
            return true;
        }

        const string completedSuffix = "_completed";
        if (fieldId.EndsWith(completedSuffix, StringComparison.OrdinalIgnoreCase))
        {
            var candidate = fieldId[..^completedSuffix.Length];
            if (lookup.TaskIds.Contains(candidate))
            {
                taskId = candidate;
                return true;
            }
        }

        string? bestMatchTaskId = null;
        var bestMatchLength = -1;
        foreach (var (knownFieldId, fieldMeta) in lookup.Fields)
        {
            if (string.IsNullOrWhiteSpace(fieldMeta.TaskId)
                || fieldId.Length <= knownFieldId.Length
                || !fieldId.StartsWith(knownFieldId + "_", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (knownFieldId.Length > bestMatchLength)
            {
                bestMatchLength = knownFieldId.Length;
                bestMatchTaskId = fieldMeta.TaskId;
            }
        }

        if (bestMatchTaskId != null)
        {
            taskId = bestMatchTaskId;
            return true;
        }

        taskId = string.Empty;
        return false;
    }

    private static void AddCollectionFlow(
        TemplateFieldLookup lookup,
        string fieldId,
        string? title,
        string? flowId,
        List<Page>? pages,
        string? taskId)
    {
        if (string.IsNullOrWhiteSpace(fieldId))
        {
            return;
        }

        var nested = new Dictionary<string, FieldMetadata>(StringComparer.OrdinalIgnoreCase);
        AddPages(nested, pages, taskId);
        // Also register nested fields in the flat lookup for any direct references
        AddPages(lookup.Fields, pages, taskId);

        lookup.CollectionNestedFields[fieldId] = nested;

        var question = ResolveFlowQuestion(title, pages, flowId);
        if (!lookup.Fields.ContainsKey(fieldId))
        {
            lookup.Fields[fieldId] = new FieldMetadata(question, "complexField", taskId);
        }
        else if (string.IsNullOrWhiteSpace(lookup.Fields[fieldId].Question) &&
                 !string.IsNullOrWhiteSpace(question))
        {
            lookup.Fields[fieldId] = new FieldMetadata(
                question,
                lookup.Fields[fieldId].TemplateFieldType ?? "complexField",
                lookup.Fields[fieldId].TaskId ?? taskId);
        }
        else if (string.IsNullOrWhiteSpace(lookup.Fields[fieldId].TaskId) &&
                 !string.IsNullOrWhiteSpace(taskId))
        {
            var existing = lookup.Fields[fieldId];
            lookup.Fields[fieldId] = existing with { TaskId = taskId };
        }
    }

    private static string ResolveFlowQuestion(string? title, List<Page>? pages, string? flowId)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title.Trim();
        }

        var firstPageTitle = pages?
            .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.Title))
            ?.Title;
        if (!string.IsNullOrWhiteSpace(firstPageTitle))
        {
            return firstPageTitle!;
        }

        return flowId?.Trim() ?? string.Empty;
    }

    private static void AddPages(Dictionary<string, FieldMetadata> target, List<Page>? pages, string? taskId)
    {
        if (pages == null)
        {
            return;
        }

        foreach (var page in pages)
        {
            if (page.Fields == null)
            {
                continue;
            }

            foreach (var field in page.Fields)
            {
                if (string.IsNullOrWhiteSpace(field.FieldId))
                {
                    continue;
                }

                var label = field.Label?.Value?.Trim();
                var question = !string.IsNullOrWhiteSpace(label)
                    ? label!
                    : (page.Title ?? string.Empty);

                // Prefer first definition if duplicates exist
                if (!target.ContainsKey(field.FieldId))
                {
                    target[field.FieldId] = new FieldMetadata(question, field.Type, taskId);
                }
            }
        }
    }
}
