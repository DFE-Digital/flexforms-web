using System.Text.Json;
using System.Text.RegularExpressions;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

/// <summary>
/// Success-message templates previously built in the PageModel/DisplayHelpers.
/// Keep the strings identical.
/// </summary>
internal static class FormEngineSuccessMessages
{
    public static Dictionary<string, object>? ExpandEncodedJson(Dictionary<string, object>? itemData)
    {
        if (itemData == null)
            return null;

        var expanded = new Dictionary<string, object>();
        foreach (var kvp in itemData)
            expanded[kvp.Key] = TransformEncodedJsonString(kvp.Value);
        return expanded;
    }

    public static string Generate(string? customMessage, string operation, Dictionary<string, object>? itemData, string? flowTitle)
    {
        if (!string.IsNullOrEmpty(customMessage))
        {
            customMessage = customMessage.Replace("{flowTitle}", flowTitle ?? "collection");
            return Interpolate(customMessage, itemData);
        }

        var displayName = GetDisplayNameFromItemData(itemData);
        var lowerFlowTitle = flowTitle?.ToLowerInvariant() ?? "collection";

        return operation switch
        {
            "add" => $"{displayName} has been added to {lowerFlowTitle}",
            "update" => $"{displayName} has been updated",
            "delete" => $"{displayName} has been removed from {lowerFlowTitle}",
            _ => $"{displayName} has been processed"
        };
    }

    private static object TransformEncodedJsonString(object value)
    {
        if (value is JsonElement { ValueKind: JsonValueKind.String } jsonString)
        {
            try
            {
                return JsonSerializer.Deserialize<JsonElement>(jsonString.GetString() ?? "") ;
            }
            catch (JsonException)
            {
                return value;
            }
        }

        return value;
    }

    private static string Interpolate(string message, Dictionary<string, object>? itemData)
    {
        if (itemData == null || itemData.Count == 0)
            return message;

        return PlaceholderRegex().Replace(message, match =>
        {
            var key = match.Groups[1].Value;
            if (itemData.TryGetValue(key, out var value) && value != null)
                return value.ToString() ?? match.Value;
            return match.Value;
        });
    }

    private static string GetDisplayNameFromItemData(Dictionary<string, object>? itemData)
    {
        var displayName = "Item";
        if (itemData == null || itemData.Count == 0)
            return displayName;

        var nameFields = new[] { "firstName", "name", "title", "label" };
        var nameField = nameFields.FirstOrDefault(field =>
            itemData.ContainsKey(field) && !string.IsNullOrEmpty(itemData[field]?.ToString()));

        if (nameField != null)
            return itemData[nameField]?.ToString() ?? "Item";

        var firstValue = itemData.Values.FirstOrDefault(v => !string.IsNullOrEmpty(v?.ToString()));
        return firstValue?.ToString() ?? "Item";
    }

    private static Regex PlaceholderRegex() => new(@"\{([^{}]+)\}", RegexOptions.CultureInvariant);
}
