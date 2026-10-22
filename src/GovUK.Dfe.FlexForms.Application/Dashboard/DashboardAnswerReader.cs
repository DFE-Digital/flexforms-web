using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace GovUK.Dfe.FlexForms.Application.Dashboard;

/// <summary>
/// Reads plain-text display values from an application response body for dashboard cells.
/// </summary>
public static class DashboardAnswerReader
{
    /// <summary>
    /// Parses <paramref name="responseBody"/> (plain JSON or base64 JSON) into a field map.
    /// </summary>
    public static IReadOnlyDictionary<string, object> ParseFormData(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        var json = DecodeResponseBody(responseBody);
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (raw is null || raw.Count == 0)
                return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            var formData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, element) in raw)
            {
                if (key.StartsWith("TaskStatus_", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (element.ValueKind == JsonValueKind.Object
                    && element.TryGetProperty("value", out var valueElement))
                {
                    formData[key] = GetJsonElementValue(valueElement);
                }
                else
                {
                    formData[key] = GetJsonElementValue(element);
                }
            }

            return formData;
        }
        catch
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Returns a plain-text value suitable for a GOV.UK table cell (no HTML).
    /// Supports top-level fields, dotted paths (<c>field.property</c>), and fields
    /// nested inside multi-collection-flow item arrays.
    /// </summary>
    public static string GetDisplayValue(string fieldPath, IReadOnlyDictionary<string, object> formData)
    {
        if (string.IsNullOrWhiteSpace(fieldPath))
            return string.Empty;

        // Exact key match (rare, but keep behaviour for odd keys containing dots).
        if (!fieldPath.Contains('[') && formData.TryGetValue(fieldPath, out var exact) && exact is not null)
            return FormatValue(exact);

        var pathSegments = ParseFieldPath(fieldPath);
        if (pathSegments.Count == 0)
            return string.Empty;

        var first = pathSegments[0];
        var remainder = pathSegments.Skip(1).ToList();

        if (formData.TryGetValue(first.Name, out var topLevel) && topLevel is not null)
        {
            if (TryGetCollectionItems(topLevel, out var items))
            {
                items = ApplyItemIndex(items, first.Index);

                // detailsOfIncomingTrust[0].incomingTrustsSearch-field-flow.name
                if (remainder.Count == 0)
                    return FormatCollectionItems(items);

                var nested = remainder[0];
                var nestedPropertyPath = remainder.Skip(1).Select(s => s.Name).ToList();
                return JoinFromItems(items, nested.Name, nestedPropertyPath, nested.Index);
            }

            var propertyPath = remainder.Select(s => s.Name).ToList();
            return FormatValueWithPath(topLevel, propertyPath);
        }

        // Field lives inside a collection item (e.g. incomingTrustsSearch-field-flow.name).
        var propertyPathRemainder = remainder.Select(s => s.Name).ToList();
        return JoinFromAllCollections(formData, first.Name, propertyPathRemainder, first.Index);
    }

    private static string JoinFromAllCollections(
        IReadOnlyDictionary<string, object> formData,
        string fieldId,
        IReadOnlyList<string> propertyPath,
        int? collectionItemIndex = null)
    {
        var values = new List<string>();

        foreach (var raw in formData.Values)
        {
            if (!TryGetCollectionItems(raw, out var items))
                continue;

            items = ApplyItemIndex(items, collectionItemIndex);

            foreach (var item in items)
            {
                if (!TryGetItemValue(item, fieldId, out var fieldRaw) || fieldRaw is null)
                    continue;

                var formatted = FormatValueWithPath(fieldRaw, propertyPath);
                if (!string.IsNullOrWhiteSpace(formatted))
                    values.Add(formatted);
            }
        }

        return string.Join(", ", values);
    }

    private static string JoinFromItems(
        IReadOnlyList<IReadOnlyDictionary<string, object>> items,
        string fieldId,
        IReadOnlyList<string> propertyPath,
        int? fieldValueIndex = null)
    {
        var values = new List<string>();
        foreach (var item in items)
        {
            if (!TryGetItemValue(item, fieldId, out var fieldRaw) || fieldRaw is null)
                continue;

            if (fieldValueIndex is int index)
            {
                if (!TrySelectIndexedValue(fieldRaw, index, out fieldRaw) || fieldRaw is null)
                    continue;
            }

            var formatted = FormatValueWithPath(fieldRaw, propertyPath);
            if (!string.IsNullOrWhiteSpace(formatted))
                values.Add(formatted);
        }

        return string.Join(", ", values);
    }

    private readonly record struct FieldPathSegment(string Name, int? Index);

    private static List<FieldPathSegment> ParseFieldPath(string fieldPath)
    {
        var segments = new List<FieldPathSegment>();
        foreach (var segment in fieldPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryParseIndexedSegment(segment, out var name, out var index))
                continue;

            if (!string.IsNullOrWhiteSpace(name))
                segments.Add(new FieldPathSegment(name, index));
        }

        return segments;
    }

    private static bool TryParseIndexedSegment(string segment, out string name, out int? index)
    {
        index = null;
        name = segment;
        var open = segment.IndexOf('[');
        if (open < 0)
            return true;

        var close = segment.LastIndexOf(']');
        if (close <= open)
            return true;

        name = segment[..open];
        var indexText = segment[(open + 1)..close].Trim();
        if (int.TryParse(indexText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0)
            index = parsed;

        return !string.IsNullOrWhiteSpace(name);
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object>> ApplyItemIndex(
        IReadOnlyList<IReadOnlyDictionary<string, object>> items,
        int? index)
    {
        if (index is null)
            return items;

        if (index.Value < 0 || index.Value >= items.Count)
            return Array.Empty<IReadOnlyDictionary<string, object>>();

        return new[] { items[index.Value] };
    }

    private static bool TrySelectIndexedValue(object raw, int index, out object? selected)
    {
        selected = null;
        if (!TryGetCollectionItems(raw, out var items))
            return false;

        if (index < 0 || index >= items.Count)
            return false;

        selected = items[index];
        return true;
    }

    private static string FormatCollectionItems(IReadOnlyList<IReadOnlyDictionary<string, object>> items)
    {
        var values = new List<string>();
        foreach (var item in items)
        {
            foreach (var preferred in new[] { "name", "title", "label" })
            {
                if (TryGetItemValue(item, preferred, out var raw) && raw is not null)
                {
                    var formatted = FormatValue(raw);
                    if (!string.IsNullOrWhiteSpace(formatted))
                    {
                        values.Add(formatted);
                        break;
                    }
                }
            }
        }

        return string.Join(", ", values);
    }

    private static bool TryGetCollectionItems(
        object? raw,
        out IReadOnlyList<IReadOnlyDictionary<string, object>> items)
    {
        items = Array.Empty<IReadOnlyDictionary<string, object>>();
        if (raw is null)
            return false;

        if (raw is string s)
        {
            var trimmed = NormalizeJsonText(s);
            if (!trimmed.StartsWith('['))
                return false;

            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    return false;

                items = ParseItemArray(doc.RootElement);
                return items.Count > 0 || doc.RootElement.GetArrayLength() == 0;
            }
            catch
            {
                return false;
            }
        }

        if (raw is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Array)
            {
                items = ParseItemArray(je);
                return true;
            }

            if (je.ValueKind == JsonValueKind.String)
                return TryGetCollectionItems(je.GetString(), out items);

            return false;
        }

        if (raw is IEnumerable<object> list)
        {
            var parsed = new List<IReadOnlyDictionary<string, object>>();
            foreach (var entry in list)
            {
                if (TryParseItem(entry, out var item))
                    parsed.Add(item);
            }

            if (parsed.Count == 0)
                return false;

            items = parsed;
            return true;
        }

        return false;
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object>> ParseItemArray(JsonElement array)
    {
        var items = new List<IReadOnlyDictionary<string, object>>();
        foreach (var element in array.EnumerateArray())
        {
            if (TryParseItem(GetJsonElementValue(element), out var item))
                items.Add(item);
        }

        return items;
    }

    private static bool TryParseItem(object? raw, out IReadOnlyDictionary<string, object> item)
    {
        item = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (raw is null)
            return false;

        if (raw is IReadOnlyDictionary<string, object> dict)
        {
            item = dict;
            return true;
        }

        if (raw is Dictionary<string, object> mutable)
        {
            item = mutable;
            return true;
        }

        if (raw is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Object)
            {
                item = JsonElementToDictionary(je);
                return true;
            }

            if (je.ValueKind == JsonValueKind.String)
                return TryParseItem(je.GetString(), out item);

            return false;
        }

        if (raw is string s)
        {
            var trimmed = NormalizeJsonText(s);
            if (!trimmed.StartsWith('{'))
                return false;

            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    return false;

                item = JsonElementToDictionary(doc.RootElement);
                return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private static Dictionary<string, object> JsonElementToDictionary(JsonElement element)
    {
        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in element.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Object
                && prop.Value.TryGetProperty("value", out var wrapped))
            {
                dict[prop.Name] = GetJsonElementValue(wrapped);
            }
            else
            {
                dict[prop.Name] = GetJsonElementValue(prop.Value);
            }
        }

        return dict;
    }

    private static bool TryGetItemValue(
        IReadOnlyDictionary<string, object> item,
        string fieldId,
        out object? value)
    {
        if (item.TryGetValue(fieldId, out value) && value is not null)
            return true;

        value = null;
        return false;
    }

    private static string FormatValueWithPath(object raw, IReadOnlyList<string> propertyPath)
    {
        if (propertyPath.Count == 0)
            return FormatValue(raw);

        var current = raw;
        foreach (var segment in propertyPath)
        {
            if (!TryGetProperty(current, segment, out var next) || next is null)
                return string.Empty;

            current = next;
        }

        return FormatValue(current);
    }

    private static bool TryGetProperty(object raw, string propertyName, out object? value)
    {
        value = null;

        if (raw is IReadOnlyDictionary<string, object> dict)
            return dict.TryGetValue(propertyName, out value) && value is not null;

        if (raw is Dictionary<string, object> mutable)
            return mutable.TryGetValue(propertyName, out value) && value is not null;

        var text = raw switch
        {
            string s => s,
            JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString(),
            JsonElement je when je.ValueKind == JsonValueKind.Object => je.GetRawText(),
            _ => Convert.ToString(raw)
        };

        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = NormalizeJsonText(text);
        if (!trimmed.StartsWith('{') && !trimmed.StartsWith('['))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty(propertyName, out var prop))
            {
                value = GetJsonElementValue(prop);
                return true;
            }
        }
        catch
        {
            // ignore
        }

        return false;
    }

    private static string DecodeResponseBody(string responseBody)
    {
        try
        {
            var decodedBytes = Convert.FromBase64String(responseBody);
            return Encoding.UTF8.GetString(decodedBytes);
        }
        catch
        {
            return responseBody;
        }
    }

    /// <summary>
    /// Complex autocomplete values are often stored HTML-encoded (e.g. <c>&amp;quot;</c>).
    /// </summary>
    private static string NormalizeJsonText(string value)
    {
        var decoded = WebUtility.HtmlDecode(value)?.Trim() ?? string.Empty;
        // Some payloads are double-encoded.
        if (decoded.Contains("&quot;", StringComparison.Ordinal)
            || decoded.Contains("&#", StringComparison.Ordinal))
        {
            decoded = WebUtility.HtmlDecode(decoded)?.Trim() ?? decoded;
        }

        return decoded;
    }

    private static object GetJsonElementValue(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => string.Empty,
            JsonValueKind.Array => element.EnumerateArray()
                .Select(GetJsonElementValue)
                .ToList(),
            JsonValueKind.Object => element.GetRawText(),
            _ => element.ToString()
        };

    private static string FormatValue(object raw)
    {
        switch (raw)
        {
            case string s:
                return FormatScalar(s);
            case bool b:
                return b ? "Yes" : "No";
            case IEnumerable<object> list:
                return string.Join(", ", list.Select(FormatValue).Where(v => !string.IsNullOrWhiteSpace(v)));
            case JsonElement je:
                return FormatValue(GetJsonElementValue(je));
            default:
                return FormatScalar(Convert.ToString(raw) ?? string.Empty);
        }
    }

    private static string FormatScalar(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || IsPlaceholder(value))
            return string.Empty;

        var trimmed = NormalizeJsonText(value);
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            // Autocomplete / complex JSON — prefer a readable name/title property when present.
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var propName in new[] { "name", "title", "label", "text", "value" })
                    {
                        if (doc.RootElement.TryGetProperty(propName, out var prop)
                            && prop.ValueKind == JsonValueKind.String
                            && !string.IsNullOrWhiteSpace(prop.GetString()))
                        {
                            return prop.GetString()!;
                        }
                    }
                }

                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var parts = doc.RootElement.EnumerateArray()
                        .Select(el => FormatValue(GetJsonElementValue(el)))
                        .Where(v => !string.IsNullOrWhiteSpace(v));
                    return string.Join(", ", parts);
                }
            }
            catch
            {
                // Fall through to raw text.
            }
        }

        if ((trimmed.Contains('-') || trimmed.Contains('/'))
            && TryParseDisplayDate(trimmed, out var date))
        {
            return date.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
        }

        return trimmed;
    }

    private static bool TryParseDisplayDate(string value, out DateTime date) =>
        DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
        || DateTime.TryParseExact(value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
        || DateTime.TryParseExact(value, "d/M/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static bool IsPlaceholder(string value) =>
        string.Equals(value, "undefined", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "null", StringComparison.OrdinalIgnoreCase);
}
