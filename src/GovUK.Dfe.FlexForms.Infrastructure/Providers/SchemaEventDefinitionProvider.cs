using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Options;
using Microsoft.Extensions.Configuration;

namespace GovUK.Dfe.FlexForms.Infrastructure.Providers;

/// <summary>
/// Reads SchemaEvents from the request-scoped effective configuration (TenantConfig overlay).
/// </summary>
public sealed class SchemaEventDefinitionProvider(
    IRequestAppConfiguration requestAppConfiguration,
    IConfiguration hostConfiguration) : ISchemaEventDefinitionProvider
{
    public const string SectionName = "SchemaEvents";

    /// <inheritdoc />
    public SchemaEventDefinitionOptions? GetDefinition(string messageType)
    {
        if (string.IsNullOrWhiteSpace(messageType))
            return null;

        return GetAll().TryGetValue(messageType.Trim(), out var def) ? def : null;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, SchemaEventDefinitionOptions> GetAll()
    {
        var result = new Dictionary<string, SchemaEventDefinitionOptions>(StringComparer.OrdinalIgnoreCase);

        BindSection(requestAppConfiguration.GetSection(SectionName), result);
        if (result.Count == 0)
            BindSection(hostConfiguration.GetSection(SectionName), result);

        return result;
    }

    private static void BindSection(
        IConfigurationSection section,
        Dictionary<string, SchemaEventDefinitionOptions> result)
    {
        if (!section.Exists())
            return;

        foreach (var child in section.GetChildren())
        {
            var def = new SchemaEventDefinitionOptions();
            child.Bind(def);
            if (string.IsNullOrWhiteSpace(def.TopicName))
                def.TopicName = child["topicName"] ?? child["TopicName"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(def.Version))
                def.Version = child["version"] ?? child["Version"] ?? "1.0";
            def.Description ??= child["description"] ?? child["Description"];

            if (string.IsNullOrWhiteSpace(def.TopicName))
                continue;

            result[child.Key] = def;
        }
    }
}
