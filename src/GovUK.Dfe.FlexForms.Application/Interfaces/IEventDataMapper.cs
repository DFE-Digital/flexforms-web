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
    /// <typeparam name="TEvent">The event type to map to</typeparam>
    /// <param name="formData">The accumulated form data (already unwrapped by the form engine)</param>
    /// <param name="template">The form template</param>
    /// <param name="mappingId">The mapping configuration ID to use</param>
    /// <param name="applicationId">The application ID</param>
    /// <param name="applicationReference">The application reference number</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The mapped event data</returns>
    Task<TEvent> MapToEventAsync<TEvent>(
        Dictionary<string, object> formData,
        FormTemplate template,
        string mappingId,
        Guid applicationId,
        string applicationReference,
        CancellationToken cancellationToken = default) where TEvent : class;
}

