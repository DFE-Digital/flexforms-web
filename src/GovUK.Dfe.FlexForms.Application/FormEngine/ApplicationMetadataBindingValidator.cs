using System.Globalization;
using System.Text.RegularExpressions;
using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

/// <summary>
/// Checks that each <c>applicationMetaData</c> binding path points at something the template defines, using the
/// same path rules as <see cref="ApplicationMetadataResolver"/>. A path the resolver can't match renders as an empty
/// row on the task list, so these problems are reported when the template is saved instead.
/// </summary>
public static partial class ApplicationMetadataBindingValidator
{
    public static readonly IReadOnlyList<string> ApplicationKeys =
    [
        "reference",
        "version",
        "leadApplicant",
        "leadApplicantName",
        "leadApplicantEmail",
    ];

    private const int MaxListedFieldIds = 15;

    /// <summary>
    /// Field types whose answer is a single plain value, so it has no child properties and no list items to index.
    /// Other types (autocomplete, complexField, upload, and any type not listed) may store objects or lists.
    /// </summary>
    private static readonly HashSet<string> PlainValueFieldTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text", "textarea", "text-area", "email", "character-count",
        "radios", "select", "checkboxes",
        "date", "datetime", "date-time",
        "number", "integer", "decimal", "boolean",
    };

    [GeneratedRegex(@"^(?<name>[^\[\]]+)(\[(?<index>\d+)\])?$")]
    private static partial Regex SegmentPattern();

    private readonly record struct PathSegment(string Name, int? Index, string Text);

    /// <summary>
    /// Returns a description of what is wrong with <paramref name="binding"/>, or <c>null</c> when it is valid.
    /// </summary>
    public static string? Validate(string binding, FormTemplate template)
    {
        var path = binding.Trim();

        if (path.StartsWith("application.", StringComparison.OrdinalIgnoreCase))
        {
            var key = path["application.".Length..];
            if (key.StartsWith("field.", StringComparison.OrdinalIgnoreCase))
                return ValidateFieldPath(key["field.".Length..], template);

            if (ApplicationKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
                return null;

            return $"'{key}' is not a recognised application value. Use one of "
                + string.Join(", ", ApplicationKeys.Select(k => $"'application.{k}'"))
                + ", or 'field.{fieldId}' for an answer"
                + Suggestion(path, ApplicationKeys.Select(k => $"application.{k}"));
        }

        if (path.StartsWith("field.", StringComparison.OrdinalIgnoreCase))
            path = path["field.".Length..];

        return ValidateFieldPath(path, template);
    }

    private static string? ValidateFieldPath(string path, FormTemplate template)
    {
        if (path.Length == 0)
            return "the path does not name a field. Add a field ID, for example 'field.trustName'";

        var fields = TemplateFields.From(template);

        // The resolver matches the whole path as a key first, so field IDs that contain dots still work.
        if (!path.Contains('[') && (fields.TopLevel.ContainsKey(path) || fields.Collections.ContainsKey(path)))
            return null;

        if (!TryParse(path, out var segments, out var badSegment))
        {
            return $"'{badSegment}' is not a valid path. Separate field IDs and properties with single dots and "
                + "use an optional zero-based index, for example 'detailsOfIncomingTrust[0].trustSearch.name'";
        }

        var first = segments[0];
        var remainder = segments.Skip(1).ToList();

        if (fields.Collections.TryGetValue(first.Name, out var collectionFields))
            return ValidateCollectionPath(first, remainder, collectionFields);

        if (fields.TopLevel.TryGetValue(first.Name, out var topLevelField))
        {
            if (first.Index is not null && IsPlainValue(topLevelField))
                return IndexOnPlainValueMessage(first, topLevelField);

            return ValidatePropertyPath(topLevelField, remainder);
        }

        // The resolver also looks inside every collection item for a field with this ID.
        var nestedField = fields.Collections.Values
            .Select(inner => inner.GetValueOrDefault(first.Name))
            .FirstOrDefault(field => field is not null);
        if (nestedField is not null)
            return ValidatePropertyPath(nestedField, remainder);

        return $"no field or collection with ID '{first.Name}' exists in the template" + Suggestion(first.Name, fields.AllIds());
    }

    private static string? ValidateCollectionPath(
        PathSegment collection,
        List<PathSegment> remainder,
        IReadOnlyDictionary<string, Field> collectionFields)
    {
        if (remainder.Count == 0)
            return null;

        var nested = remainder[0];
        if (!collectionFields.TryGetValue(nested.Name, out var field))
        {
            var available = collectionFields.Count == 0
                ? " That collection has no fields"
                : $" Fields in that collection: {FormatIds(collectionFields.Keys)}";
            return $"'{nested.Name}' is not a field in collection '{collection.Name}'."
                + available
                + Suggestion(nested.Name, collectionFields.Keys);
        }

        if (nested.Index is not null && IsPlainValue(field))
            return IndexOnPlainValueMessage(nested, field);

        return ValidatePropertyPath(field, remainder.Skip(1).ToList());
    }

    private static string? ValidatePropertyPath(Field field, List<PathSegment> propertyPath)
    {
        // Lookup and upload answers come from external data, so their child properties can't be checked here.
        if (propertyPath.Count == 0 || !IsPlainValue(field))
            return null;

        var property = string.Join(".", propertyPath.Select(s => s.Name));
        return $"'{field.FieldId}' is a '{field.Type}' field, which stores a single value with no child properties, "
            + $"so '.{property}' cannot be read. Remove '.{property}', or point at a lookup (autocomplete) or upload field";
    }

    private static string IndexOnPlainValueMessage(PathSegment segment, Field field) =>
        $"'{segment.Text}' cannot use an index because '{field.FieldId}' is a '{field.Type}' field, which stores a single value. "
        + $"Indexes only apply to collections and lookup fields that hold a list of items. Use '{segment.Name}' instead";

    private static bool IsPlainValue(Field field) =>
        field.ComplexField is null && PlainValueFieldTypes.Contains(field.Type?.Trim() ?? string.Empty);

    private static bool TryParse(string path, out List<PathSegment> segments, out string badSegment)
    {
        segments = [];
        badSegment = string.Empty;

        foreach (var raw in path.Split('.'))
        {
            var text = raw.Trim();
            var match = SegmentPattern().Match(text);
            if (!match.Success || string.IsNullOrWhiteSpace(match.Groups["name"].Value))
            {
                badSegment = text.Length == 0 ? path : text;
                return false;
            }

            int? index = match.Groups["index"].Success
                ? int.Parse(match.Groups["index"].Value, NumberStyles.None, CultureInfo.InvariantCulture)
                : null;
            segments.Add(new PathSegment(match.Groups["name"].Value.Trim(), index, text));
        }

        return segments.Count > 0;
    }

    private static string Suggestion(string id, IEnumerable<string> candidates)
    {
        var best = candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(candidate => (Candidate: candidate, Distance: EditDistance(id.ToLowerInvariant(), candidate.ToLowerInvariant())))
            .Where(x => x.Distance <= Math.Max(2, id.Length / 3))
            .OrderBy(x => x.Distance)
            .FirstOrDefault();

        return best.Candidate is null ? string.Empty : $". Did you mean '{best.Candidate}'?";
    }

    private static string FormatIds(IEnumerable<string> ids)
    {
        var list = ids.ToList();
        var shown = string.Join(", ", list.Take(MaxListedFieldIds).Select(id => $"'{id}'"));
        return list.Count > MaxListedFieldIds ? $"{shown} and {list.Count - MaxListedFieldIds} more" : shown;
    }

    private static int EditDistance(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
            previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }

    /// <summary>
    /// Fields on task pages, and collections (the <c>fieldId</c> of each multi- or derived-collection flow) with the
    /// fields on their pages. Answer lookups ignore case, so these do too.
    /// </summary>
    private sealed class TemplateFields
    {
        public Dictionary<string, Field> TopLevel { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, Dictionary<string, Field>> Collections { get; } = new(StringComparer.OrdinalIgnoreCase);

        public IEnumerable<string> AllIds() =>
            TopLevel.Keys
                .Concat(Collections.Keys)
                .Concat(Collections.Values.SelectMany(inner => inner.Keys));

        public static TemplateFields From(FormTemplate template)
        {
            var result = new TemplateFields();

            foreach (var task in (template.TaskGroups ?? []).SelectMany(group => group.Tasks ?? []))
            {
                AddPageFields(task.Pages, result.TopLevel);

                var summary = task.Summary;
                if (summary is null)
                    continue;

                var flows = (summary.Flows ?? []).Select(flow => (flow.FieldId, flow.Pages))
                    .Concat((summary.DerivedFlows ?? []).Select(flow => (flow.FieldId, flow.Pages)));

                foreach (var (collectionId, pages) in flows)
                {
                    if (string.IsNullOrWhiteSpace(collectionId))
                        continue;

                    if (!result.Collections.TryGetValue(collectionId.Trim(), out var inner))
                    {
                        inner = new Dictionary<string, Field>(StringComparer.OrdinalIgnoreCase);
                        result.Collections[collectionId.Trim()] = inner;
                    }

                    AddPageFields(pages, inner);
                }
            }

            return result;
        }

        private static void AddPageFields(IEnumerable<Page>? pages, Dictionary<string, Field> target)
        {
            foreach (var field in (pages ?? []).SelectMany(page => page.Fields ?? []))
            {
                if (!string.IsNullOrWhiteSpace(field.FieldId))
                    target.TryAdd(field.FieldId.Trim(), field);
            }
        }
    }
}
