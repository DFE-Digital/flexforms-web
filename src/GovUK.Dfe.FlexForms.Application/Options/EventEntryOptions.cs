namespace GovUK.Dfe.FlexForms.Application.Options;

/// <summary>
/// One event to publish: event type name and mapping configuration id.
/// </summary>
public class EventEntryOptions
{
    /// <summary>
    /// Event type name (e.g. "TransferApplicationSubmittedEvent"). Used to resolve .NET type and mapping file.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Mapping configuration id (e.g. "transfer-application-submitted-v1").
    /// </summary>
    public string MappingId { get; set; } = string.Empty;
}
