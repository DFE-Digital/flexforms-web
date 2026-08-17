using System.Text.Json;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

/// <summary>
/// Reads collection-flow item lists stored as JSON in accumulated form data.
/// </summary>
public static class FormEngineCollectionItems
{
    public static List<Dictionary<string, object>> Read(Dictionary<string, object> formData, string fieldId)
    {
        if (!formData.TryGetValue(fieldId, out var value) || value == null)
            return [];

        var json = value.ToString();
        if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith('['))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
