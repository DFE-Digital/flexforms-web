namespace GovUK.Dfe.FlexForms.Domain.Models.Messaging;

/// <summary>
/// Envelope published for tenant-defined schema events (Phase 3).
/// Downstream consumers filter on <see cref="MessageType"/> / headers.
/// </summary>
public sealed class SchemaEventEnvelope
{
    public required string MessageType { get; init; }

    public required string Version { get; init; }

    public required string TopicName { get; init; }

    public required Dictionary<string, object?> Payload { get; init; }

    public Dictionary<string, object?>? Metadata { get; init; }
}
