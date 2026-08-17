using System.Text;
using System.Text.Json;

namespace GovUK.Dfe.FlexForms.Web.ViewModels.FormEngine;

/// <summary>
/// Formats autocomplete JSON objects into the HTML used on confirmation and preview pages.
/// </summary>
public static class AutocompleteSummaryFormatter
{
    public static string Render(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(rawValue);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return System.Net.WebUtility.HtmlEncode(rawValue);

            var root = doc.RootElement;
            var name = root.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
                ? n.GetString() ?? string.Empty
                : string.Empty;
            var postcode = root.TryGetProperty("postcode", out var pc) && pc.ValueKind == JsonValueKind.String
                ? pc.GetString() ?? string.Empty
                : string.Empty;
            if (string.IsNullOrWhiteSpace(postcode)
                && root.TryGetProperty("postCode", out var pc2)
                && pc2.ValueKind == JsonValueKind.String)
            {
                postcode = pc2.GetString() ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(postcode)
                && root.TryGetProperty("address", out var addr)
                && addr.ValueKind == JsonValueKind.Object)
            {
                if (addr.TryGetProperty("postcode", out var apc) && apc.ValueKind == JsonValueKind.String)
                    postcode = apc.GetString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(postcode)
                    && addr.TryGetProperty("postCode", out var apc2)
                    && apc2.ValueKind == JsonValueKind.String)
                {
                    postcode = apc2.GetString() ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(postcode)
                    && addr.TryGetProperty("postalCode", out var apc3)
                    && apc3.ValueKind == JsonValueKind.String)
                {
                    postcode = apc3.GetString() ?? string.Empty;
                }
            }

            var ukprn = root.TryGetProperty("ukprn", out var u) ? u.ToString() : string.Empty;
            var companiesHouse = root.TryGetProperty("companiesHouseNumber", out var c)
                && c.ValueKind == JsonValueKind.String
                    ? c.GetString() ?? string.Empty
                    : string.Empty;
            if (string.IsNullOrWhiteSpace(companiesHouse) && root.TryGetProperty("companiesHousenumber", out var c2))
                companiesHouse = c2.ToString();

            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(name))
                sb.Append($"<strong class=\"govuk-!-font-weight-bold\">{System.Net.WebUtility.HtmlEncode(name)}</strong>");
            if (!string.IsNullOrWhiteSpace(postcode))
                sb.Append($"<br/>Postcode: {System.Net.WebUtility.HtmlEncode(postcode)}");
            if (!string.IsNullOrWhiteSpace(ukprn))
                sb.Append($"<br/>UKPRN: {System.Net.WebUtility.HtmlEncode(ukprn)}");
            if (!string.IsNullOrWhiteSpace(companiesHouse))
                sb.Append($"<br/>Companies house number: {System.Net.WebUtility.HtmlEncode(companiesHouse)}");
            return sb.ToString();
        }
        catch (JsonException)
        {
            return System.Net.WebUtility.HtmlEncode(rawValue);
        }
    }

    public static string TryFindJsonInItem(Dictionary<string, object> item)
    {
        foreach (var kv in item)
        {
            var s = kv.Value?.ToString();
            if (string.IsNullOrWhiteSpace(s))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(s);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    continue;

                if (doc.RootElement.TryGetProperty("name", out _)
                    || doc.RootElement.TryGetProperty("ukprn", out _)
                    || doc.RootElement.TryGetProperty("companiesHouseNumber", out _))
                {
                    return s;
                }
            }
            catch (JsonException)
            {
                // Value is not autocomplete JSON; keep scanning other fields.
            }
        }

        return string.Empty;
    }
}
