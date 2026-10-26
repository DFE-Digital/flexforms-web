namespace GovUK.Dfe.FlexForms.Application.Interfaces;

/// <summary>
/// An answer id that an event mapping reads from a template.
/// </summary>
public sealed record EventMappingFieldReference(string FieldId, string EventType, string MappingId);

public interface IEventMappingReferenceReader
{
    /// <summary>
    /// Every answer id the current tenant's event mappings read from the template. Mappings are stored under the
    /// template's GUID or under the templateId in its JSON, so both keys are checked.
    /// </summary>
    IReadOnlyList<EventMappingFieldReference> GetFieldReferences(IEnumerable<string> templateKeys);
}
