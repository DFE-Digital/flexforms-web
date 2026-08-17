using System.Text.RegularExpressions;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

public sealed class PostedFormDataBinder : IPostedFormDataBinder
{
    private static readonly Regex DataFieldRegex = new(
        @"^Data\[(.+?)\]$",
        RegexOptions.None,
        TimeSpan.FromMilliseconds(200));

    private static readonly Regex DatePartRegex = new(
        @"^Data\[(.+?)\](?:[.\-](day|month|year))$",
        RegexOptions.IgnoreCase,
        TimeSpan.FromMilliseconds(200));

    public Dictionary<string, object> Bind(
        IReadOnlyDictionary<string, IReadOnlyList<string>> formFields,
        Dictionary<string, object>? existing = null)
    {
        var data = existing ?? new Dictionary<string, object>();

        foreach (var (key, values) in formFields)
        {
            var match = DataFieldRegex.Match(key);
            if (!match.Success)
                continue;

            var fieldId = match.Groups[1].Value;
            var normalisedFieldId = fieldId.StartsWith("Data_", StringComparison.Ordinal)
                ? fieldId[5..]
                : fieldId;

            object bound = values.Count switch
            {
                1 => HtmlInputSanitiser.Sanitise(values[0] ?? string.Empty),
                > 1 => values.Select(v => HtmlInputSanitiser.Sanitise(v ?? string.Empty)).ToArray(),
                _ => string.Empty
            };

            data[fieldId] = bound;
            if (!string.Equals(fieldId, normalisedFieldId, StringComparison.Ordinal))
                data[normalisedFieldId] = bound;
        }

        return data;
    }

    public void ApplyDateParts(
        IReadOnlyDictionary<string, IReadOnlyList<string>> formFields,
        Dictionary<string, object> data)
    {
        var dateParts = new Dictionary<string, (string? Day, string? Month, string? Year)>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, values) in formFields)
        {
            var dateMatch = DatePartRegex.Match(key);
            if (!dateMatch.Success)
                continue;

            var dateFieldId = dateMatch.Groups[1].Value;
            var part = dateMatch.Groups[2].Value.ToLowerInvariant();
            var formValue = values.Count > 0 ? values[0] : string.Empty;

            if (!dateParts.TryGetValue(dateFieldId, out var parts))
                parts = (null, null, null);

            parts = part switch
            {
                "day" => (formValue, parts.Month, parts.Year),
                "month" => (parts.Day, formValue, parts.Year),
                "year" => (parts.Day, parts.Month, formValue),
                _ => parts
            };

            dateParts[dateFieldId] = parts;
        }

        foreach (var (fieldId, parts) in dateParts)
        {
            var anyEntered = !string.IsNullOrWhiteSpace(parts.Day)
                || !string.IsNullOrWhiteSpace(parts.Month)
                || !string.IsNullOrWhiteSpace(parts.Year);
            if (!anyEntered)
                continue;

            var normalisedFieldId = fieldId.StartsWith("Data_", StringComparison.Ordinal) ? fieldId[5..] : fieldId;
            string composed;
            if (int.TryParse(parts.Year, out var y)
                && int.TryParse(parts.Month, out var m)
                && int.TryParse(parts.Day, out var d))
            {
                var yearText = parts.Year?.Trim() ?? string.Empty;
                if (yearText.Length != 4)
                {
                    composed = $"{parts.Year}-{parts.Month}-{parts.Day}";
                }
                else
                {
                    try
                    {
                        composed = new DateTime(y, m, d).ToString("yyyy-MM-dd");
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        composed = $"{parts.Year}-{parts.Month}-{parts.Day}";
                    }
                }
            }
            else
            {
                composed = $"{parts.Year}-{parts.Month}-{parts.Day}";
            }

            data[fieldId] = composed;
            if (!string.Equals(fieldId, normalisedFieldId, StringComparison.Ordinal))
                data[normalisedFieldId] = composed;
        }
    }
}
