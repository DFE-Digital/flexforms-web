namespace GovUK.Dfe.FlexForms.Application.Interfaces;

/// <summary>
/// Resolves event type name (from config) to .NET Type for use with IEventDataMapper.MapToEventAsync.
/// </summary>
public interface IEventTypeRegistry
{
    /// <summary>
    /// Gets the .NET type for the given event type name (e.g. "TransferApplicationSubmittedEvent").
    /// </summary>
    /// <param name="eventTypeName">Event type name as configured in appsettings</param>
    /// <returns>The Type to use for mapping and publishing, or null if not registered</returns>
    Type? GetEventType(string eventTypeName);
}
