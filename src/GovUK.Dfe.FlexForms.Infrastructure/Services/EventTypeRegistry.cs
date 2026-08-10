using System.Collections.Concurrent;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Models;
using GovUK.Dfe.FlexForms.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services;

/// <summary>
/// Maps event type names (from config) to .NET types by scanning CoreLibs Messaging.Contracts.
/// </summary>
public class EventTypeRegistry : IEventTypeRegistry
{
    private readonly ConcurrentDictionary<string, Type> _eventTypes;
    private readonly IReadOnlyList<EventCatalogueEntry> _catalogue;

    /// <summary>
    /// Creates a registry populated from assembly-scanned messaging contracts.
    /// </summary>
    public EventTypeRegistry(ILogger<EventTypeRegistry>? logger = null)
    {
        var discovered = MessagingEventDiscovery.Discover();
        _eventTypes = new ConcurrentDictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        var catalogue = new List<EventCatalogueEntry>(discovered.Count);

        foreach (var entry in discovered)
        {
            _eventTypes[entry.EventTypeName] = entry.ClrType;
            catalogue.Add(new EventCatalogueEntry(entry.EventTypeName, entry.TopicName, entry.ClrType));

            if (entry.TopicName is null)
            {
                logger?.LogWarning(
                    "Discovered messaging event {EventType} has no matching TopicNames constant; MassTransit topic wiring will be skipped.",
                    entry.EventTypeName);
            }
        }

        _catalogue = catalogue;

        logger?.LogInformation(
            "EventTypeRegistry loaded {Count} event type(s) from Messaging.Contracts.",
            _catalogue.Count);
    }

    /// <summary>
    /// Registers an additional event type by its type (uses type.Name as the key).
    /// </summary>
    public void Register(Type eventType)
    {
        if (eventType == null) throw new ArgumentNullException(nameof(eventType));
        _eventTypes[eventType.Name] = eventType;
    }

    /// <inheritdoc />
    public Type? GetEventType(string eventTypeName)
    {
        if (string.IsNullOrEmpty(eventTypeName)) return null;
        return _eventTypes.TryGetValue(eventTypeName, out var type) ? type : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<EventCatalogueEntry> GetCatalogue() => _catalogue;
}
