namespace GovUK.Dfe.FlexForms.Infrastructure.Messaging;

/// <summary>
/// An event type discovered from CoreLibs Messaging.Contracts with its ASB topic name.
/// </summary>
public sealed record DiscoveredMessagingEvent(
    Type ClrType,
    string EventTypeName,
    string? TopicName);
