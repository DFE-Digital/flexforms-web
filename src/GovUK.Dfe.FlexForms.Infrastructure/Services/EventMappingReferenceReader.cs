using GovUK.Dfe.FlexForms.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services;

/// <summary>
/// Reads the EventMappings tenant setting: template key, then event type, then the mapping.
/// </summary>
public sealed class EventMappingReferenceReader(IRequestAppConfiguration requestConfiguration) : IEventMappingReferenceReader
{
    private const string SectionName = "EventMappings";

    private static readonly string[] SingleFieldKeys = ["sourceFieldId", "sourceCollectionFieldId"];

    public IReadOnlyList<EventMappingFieldReference> GetFieldReferences(IEnumerable<string> templateKeys)
    {
        var mappings = requestConfiguration.Current.GetSection(SectionName);
        var references = new List<EventMappingFieldReference>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var templateKey in templateKeys.Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var mapping in mappings.GetSection(templateKey).GetChildren())
            {
                var eventType = mapping.Key;
                var mappingId = mapping["mappingId"] ?? eventType;
                foreach (var fieldId in FieldIdsIn(mapping))
                {
                    if (seen.Add($"{eventType}|{fieldId}"))
                        references.Add(new EventMappingFieldReference(fieldId, eventType, mappingId));
                }
            }
        }

        return references;
    }

    private static IEnumerable<string> FieldIdsIn(IConfigurationSection section)
    {
        foreach (var child in section.GetChildren())
        {
            if (SingleFieldKeys.Contains(child.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(child.Value))
                    yield return child.Value.Trim();
                continue;
            }

            if (string.Equals(child.Key, "sourceFieldIds", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var item in child.GetChildren())
                {
                    if (!string.IsNullOrWhiteSpace(item.Value))
                        yield return item.Value.Trim();
                }
                continue;
            }

            foreach (var nested in FieldIdsIn(child))
                yield return nested;
        }
    }
}
