using System.Globalization;
using System.Text.Json;
using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services;

/// <summary>
/// Resolves question text and dataType for response JSON field entries from the template,
/// with runtime value fallback for dataType when the template type is unavailable.
/// </summary>
public static class ResponseFieldMetadataResolver
{
    public sealed record FieldMetadata(string Question, string? TemplateFieldType);

    public static Dictionary<string, FieldMetadata> BuildLookup(FormTemplate? template)
    {
        var lookup = new Dictionary<string, FieldMetadata>(StringComparer.OrdinalIgnoreCase);
        if (template?.TaskGroups == null)
        {
            return lookup;
        }

        foreach (var task in template.TaskGroups.SelectMany(g => g.Tasks))
        {
            AddPages(lookup, task.Pages);

            if (task.Summary?.Flows != null)
            {
                foreach (var flow in task.Summary.Flows)
                {
                    AddFlowField(lookup, flow.FieldId, flow.Title);
                    AddPages(lookup, flow.Pages);
                }
            }

            if (task.Summary?.DerivedFlows != null)
            {
                foreach (var flow in task.Summary.DerivedFlows)
                {
                    AddFlowField(lookup, flow.FieldId, flow.Title);
                    AddPages(lookup, flow.Pages);
                }
            }
        }

        return lookup;
    }

    public static string ResolveQuestion(string fieldId, Dictionary<string, FieldMetadata> lookup)
    {
        return lookup.TryGetValue(fieldId, out var metadata)
            ? metadata.Question
            : string.Empty;
    }

    public static string ResolveDataType(
        string fieldId,
        string? value,
        Dictionary<string, FieldMetadata> lookup)
    {
        if (lookup.TryGetValue(fieldId, out var metadata) &&
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

    public static object BuildFormFieldEntry(
        string fieldId,
        string value,
        Dictionary<string, FieldMetadata> lookup)
    {
        return new
        {
            question = ResolveQuestion(fieldId, lookup),
            value,
            completed = !string.IsNullOrWhiteSpace(value),
            dataType = ResolveDataType(fieldId, value, lookup)
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
    private static void AddPages(Dictionary<string, FieldMetadata> lookup, List<Page>? pages)
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
                if (!lookup.ContainsKey(field.FieldId))
                {
                    lookup[field.FieldId] = new FieldMetadata(question, field.Type);
                }
            }
        }
    }

    private static void AddFlowField(Dictionary<string, FieldMetadata> lookup, string fieldId, string? title)
    {
        if (string.IsNullOrWhiteSpace(fieldId) || lookup.ContainsKey(fieldId))
        {
            return;
        }

        lookup[fieldId] = new FieldMetadata(title?.Trim() ?? string.Empty, "complexField");
    }
}
