using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.CoreLibs.Messaging.Contracts.Messages.Events;
using System.Collections.Concurrent;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services;

/// <summary>
/// Maps event type names (from config) to .NET types for use with IEventDataMapper.MapToEventAsync.
/// Register additional event types via Register when new message types are added.
/// </summary>
public class EventTypeRegistry : IEventTypeRegistry
{
    private readonly ConcurrentDictionary<string, Type> _eventTypes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a registry with the built-in event types registered.
    /// </summary>
    public EventTypeRegistry()
    {
        Register(typeof(TransferApplicationSubmittedEvent));
    }

    /// <summary>
    /// Registers an event type by its type (uses type.Name as the key).
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
}
