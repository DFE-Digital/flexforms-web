namespace GovUK.Dfe.FlexForms.Application.Options;

/// <summary>
/// One event to publish: event type name and mapping configuration id.
/// </summary>
public class EventEntryOptions
{
    /// <summary>
    /// <see cref="EventPublishKind.Typed"/> (default) or <see cref="EventPublishKind.Schema"/>.
    /// </summary>
    public string EventKind { get; set; } = EventPublishKind.Typed;

    /// <summary>
    /// Event type name (e.g. "TransferApplicationSubmittedEvent" or a SchemaEvents key).
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Mapping configuration id (e.g. "transfer-application-submitted-v1").
    /// </summary>
    public string MappingId { get; set; } = string.Empty;
}
