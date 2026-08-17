namespace GovUK.Dfe.FlexForms.Application.Admin;

public sealed record EventCatalogueRow(
    string EventTypeName,
    string TopicName,
    string ClrTypeName,
    string? Description,
    string Version,
    string Kind,
    IReadOnlyList<string> Properties);

public sealed record SchemaEventRow(
    string MessageType,
    string TopicName,
    string Version,
    string? Description);

public sealed record SavedMappingRow(
    string TemplateId,
    string EventType,
    string MappingId,
    string? Description);

public sealed record TriggerBindingRow(
    string Trigger,
    string EventKind,
    string EventType,
    string MappingId);

public sealed record MetadataKeyHint(string Key, string Description);
