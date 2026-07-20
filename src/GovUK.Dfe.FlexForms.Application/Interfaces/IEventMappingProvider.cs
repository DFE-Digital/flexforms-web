using GovUK.Dfe.FlexForms.Domain.Models.EventMapping;

namespace GovUK.Dfe.FlexForms.Application.Interfaces;

/// <summary>
/// Provides event field mapping configurations
/// </summary>
public interface IEventMappingProvider
{
    /// <summary>
    /// Gets the event mapping configuration for a specific event type and template
    /// </summary>
    /// <param name="templateId">The form template ID</param>
    /// <param name="eventType">The event type name (e.g., "TransferApplicationSubmittedEvent")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The mapping configuration, or null if not found</returns>
    Task<EventFieldMapping?> GetMappingAsync(
        string templateId,
        string eventType,
        CancellationToken cancellationToken = default);
}

