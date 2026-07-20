using System.Text.Json;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services;

/// <summary>
/// Normalises checkbox values into a simple string collection regardless of source shape.
/// </summary>
public static class CheckboxValueNormalizer
{
    /// <summary>
    /// Converts common checkbox payload shapes into a string collection (ignores null/empty entries).
    /// </summary>
    public static IReadOnlyCollection<string> Normalize(object? value)
    {
        if (value is null)
        {
            return Array.Empty<string>();
        }

        if (value is string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return Array.Empty<string>();
            }

            var trimmed = s.Trim();
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        return doc.RootElement
                            .EnumerateArray()
                            .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : e.ToString())
                            .Where(v => !string.IsNullOrWhiteSpace(v))
                            .ToArray();
                    }
                }
                catch
                {
                    // Fall through to return the raw string if parsing fails
                }
            }

            return new[] { s };
        }

        if (value is IEnumerable<string> stringEnumerable && value is not string)
        {
            return stringEnumerable.Where(v => !string.IsNullOrWhiteSpace(v)).ToArray();
        }

        if (value is JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.Array)
            {
                return json
                    .EnumerateArray()
                    .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : e.ToString())
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .ToArray();
            }

            if (json.ValueKind == JsonValueKind.String)
            {
                var str = json.GetString();
                return string.IsNullOrWhiteSpace(str) ? Array.Empty<string>() : new[] { str };
            }
        }

        if (value is IEnumerable<object> objEnumerable)
        {
            return objEnumerable
                .Select(o => o?.ToString())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToArray();
        }

        var fallback = value.ToString();
        return string.IsNullOrWhiteSpace(fallback) ? Array.Empty<string>() : new[] { fallback };
    }
}
