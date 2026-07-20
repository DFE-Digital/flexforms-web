namespace GovUK.Dfe.FlexForms.Application.Interfaces;

/// <summary>
/// Resolves the configured IEventDataMapper for the current application (based on ApplicationSubmission:MapperKey).
/// </summary>
public interface IEventDataMapperFactory
{
    /// <summary>
    /// Gets the event data mapper configured for this application.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The mapper instance to use for mapping form data to events</returns>
    IEventDataMapper GetMapper(CancellationToken cancellationToken = default);
}
