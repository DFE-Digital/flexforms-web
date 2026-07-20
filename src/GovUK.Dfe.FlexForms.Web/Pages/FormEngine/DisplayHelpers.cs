using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GovUK.Dfe.FlexForms.Web.Pages.FormEngine;

public static partial class DisplayHelpers
{
    /// <summary>
    /// Expands JSON strings encoded within a dictionary's values into their corresponding JSON objects.
    /// When a value is a JsonElement of type String containing valid JSON, it will be deserialized into a JsonElement.
    /// Other values remain unchanged.
    /// </summary>
    /// <param name="itemData">The dictionary containing potentially encoded JSON strings as values</param>
    /// <returns>A new dictionary with the same keys but with encoded JSON strings expanded into JsonElement objects.
    /// Returns null if the input dictionary is null.</returns>
    public static Dictionary<string, object>? ExpandEncodedJson(Dictionary<string, object>? itemData)
    {
        return itemData?.Select(kvp => (kvp.Key, TransformEncodedJsonString(kvp.Value))).ToDictionary();
    }

    private static object TransformEncodedJsonString(object value)
    {
        switch (value)
        {
            case JsonElement { ValueKind: JsonValueKind.String } jsonString:
                try
                {
                    return JsonSerializer.Deserialize<JsonElement>(UnsanitiseHtmlInput(jsonString.GetString() ?? ""));
                }
                catch (JsonException)
                {
                    return value;
                }
            default:
                return value;
        }
    }

    /// <summary>
    /// Generates a success message using custom template or fallback default
    /// </summary>
    /// <param name="customMessage">Custom message template from configuration</param>
    /// <param name="operation">Operation type: "add", "update", or "delete"</param>
    /// <param name="itemData">Dictionary containing all field values for the item</param>
    /// <param name="flowTitle">Title of the flow</param>
    /// <returns>Formatted success message</returns>
    public static string GenerateSuccessMessage(string? customMessage, string operation, Dictionary<string, object>? itemData, string? flowTitle)
    {
        if (!string.IsNullOrEmpty(customMessage))
        {
            customMessage = customMessage.Replace("{flowTitle}", flowTitle ?? "collection");
            return InterpolateMessage(customMessage, itemData);
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

    /// <summary>
    /// <para>
    /// Replaces placeholders in the given message with values from <c>itemData</c>.
    /// </para>
    /// <para>
    /// Placeholders are in the form of <c>{key}</c> or <c>{key.subkey}</c> where <c>key</c> is the name of a field in
    /// <c>itemData</c>.
    /// Subkeys can be used to index into properties of <c>key</c> if <c>key</c> is a JSON object.
    /// </para>
    /// </summary>
    /// <param name="message">The message to interpolate.</param>
    /// <param name="itemData">A <see cref="Dictionary&lt;string, object&gt;"/> containing properties to interpolate
    /// into the message.</param>
    /// <returns></returns>
    public static string InterpolateMessage(string message, Dictionary<string, object>? itemData)
    {
        return new InterpolatedString(message).Render(itemData ?? new Dictionary<string, object>());
    }

    private static string GetDisplayNameFromItemData(Dictionary<string, object>? itemData)
    {
        var displayName = "Item";
        if (itemData == null || itemData.Count == 0) return displayName;
        
        // Try common name fields first, then fall back to any non-empty value
        var nameFields = new[] { "firstName", "name", "title", "label" };
        var nameField = nameFields.FirstOrDefault(field => itemData.ContainsKey(field) && !string.IsNullOrEmpty(itemData[field].ToString()));
            
        if (nameField != null)
        {
            displayName = itemData[nameField].ToString() ?? "Item";
        }
        else
        {
            // Use the first non-empty field value
            var firstValue = itemData.Values.FirstOrDefault(v => !string.IsNullOrEmpty(v.ToString()));
            if (firstValue != null)
            {
                displayName = firstValue.ToString() ?? "Item";
            }
        }

        return displayName;
    }

    /// <summary>
    /// <para>
    /// Sanitises a string containing HTML by encoding its content to prevent XSS attacks.
    /// </para>
    /// <para>
    /// Removes line breaks and normalises them to the <c>&lt;br&gt;</c> tag, and encodes any HTML content or characters
    /// outside of the ASCII range.
    /// </para>
    /// </summary>
    /// <param name="input">The input string containing potentially unsafe text.</param>
    /// <returns>A sanitised string with HTML encoded content and normalised line breaks.</returns>
    public static string SanitiseHtmlInput(string input)
    {
        var lines = input.Split("\r\n").SelectMany(s => s.Split('\r')).SelectMany(s => s.Split('\n'));

        return string.Join("<br>", lines.Select(HtmlEncoder.Default.Encode));
    }

    /// <summary>
    /// Converts a sanitised HTML input string back to its original form by decoding HTML entities
    /// and replacing <c>&lt;br&gt;</c> tags with newline characters.
    /// </summary>
    /// <param name="sanitisedInput">The HTML-encoded input string with <c>&lt;br&gt;</c> used as newline indicators.</param>
    /// <returns>The original unsanitised string with HTML entities decoded and <c>&lt;br&gt;</c> replaced with newlines.</returns>
    public static string UnsanitiseHtmlInput(string sanitisedInput) =>
        HttpUtility.HtmlDecode(LineBreakTagRegex().Replace(sanitisedInput, "\n"));
    
    [GeneratedRegex(@"<br\s*/?>")]
    private static partial Regex LineBreakTagRegex();
}