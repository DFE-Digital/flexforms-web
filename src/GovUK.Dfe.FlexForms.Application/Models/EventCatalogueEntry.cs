namespace GovUK.Dfe.FlexForms.Application.Models;

/// <summary>
/// A platform event type discovered from CoreLibs Messaging.Contracts.
/// </summary>
public sealed record EventCatalogueEntry(
    string EventTypeName,
    string? TopicName,
    Type ClrType);
