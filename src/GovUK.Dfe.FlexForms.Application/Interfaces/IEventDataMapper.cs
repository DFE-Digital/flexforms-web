using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Application.Interfaces;

/// <summary>
/// Maps form data to event models based on configurations
/// </summary>
public interface IEventDataMapper
{
    /// <summary>
    /// Maps accumulated form data to a specific event type using the configured mapping
    /// </summary>
    Task<TEvent> MapToEventAsync<TEvent>(
        Dictionary<string, object> formData,
        FormTemplate template,
        string mappingId,
        Guid applicationId,
        string applicationReference,
        CancellationToken cancellationToken = default) where TEvent : class;

    /// <summary>
    /// Maps form data to a dictionary payload using the mapping for <paramref name="eventTypeName"/>
    /// (used for schema events that have no CLR contract).
    /// </summary>
    Task<Dictionary<string, object?>> MapToDictionaryAsync(
        Dictionary<string, object> formData,
        FormTemplate template,
        string eventTypeName,
        string mappingId,
        Guid applicationId,
        string applicationReference,
        CancellationToken cancellationToken = default);
}

