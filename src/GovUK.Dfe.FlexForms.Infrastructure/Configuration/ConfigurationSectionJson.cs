using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace GovUK.Dfe.FlexForms.Infrastructure.Configuration;

/// <summary>
/// Rebuilds a JSON document from a flattened <see cref="IConfigurationSection"/>
/// (as produced by TenantConfig FlattenJson).
/// </summary>
public static class ConfigurationSectionJson
{
    /// <summary>
    /// Serializes the section subtree to a JSON string, or null when the section is empty.
    /// </summary>
    public static string? ToJson(IConfigurationSection section)
    {
        ArgumentNullException.ThrowIfNull(section);

        var children = section.GetChildren().ToList();
        if (children.Count == 0)
        {
            return string.IsNullOrEmpty(section.Value) ? null : JsonSerializer.Serialize(section.Value);
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteValue(writer, section, children);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteValue(
        Utf8JsonWriter writer,
        IConfigurationSection section,
        List<IConfigurationSection>? preloadedChildren = null)
    {
        var children = preloadedChildren ?? section.GetChildren().ToList();
        if (children.Count == 0)
        {
            WriteLeaf(writer, section.Value);
            return;
        }

        if (children.All(c => int.TryParse(c.Key, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
        {
            writer.WriteStartArray();
            foreach (var child in children.OrderBy(c => int.Parse(c.Key, CultureInfo.InvariantCulture)))
            {
                WriteValue(writer, child);
            }

            writer.WriteEndArray();
            return;
        }

        writer.WriteStartObject();
        foreach (var child in children)
        {
            writer.WritePropertyName(child.Key);
            WriteValue(writer, child);
        }

        writer.WriteEndObject();
    }

    private static void WriteLeaf(Utf8JsonWriter writer, string? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        if (bool.TryParse(value, out var boolean))
        {
            writer.WriteBooleanValue(boolean);
            return;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
        {
            writer.WriteNumberValue(longValue);
            return;
        }

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
        {
            writer.WriteNumberValue(decimalValue);
            return;
        }

        writer.WriteStringValue(value);
    }
}
