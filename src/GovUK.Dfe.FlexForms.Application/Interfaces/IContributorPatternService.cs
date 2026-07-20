using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;

namespace GovUK.Dfe.FlexForms.Application.Interfaces;

/// <summary>
/// Determines whether contributor invitation is enabled for the current template.
/// </summary>
public interface IContributorPatternService
{
    /// <summary>
    /// Returns true when the template allows contributor invitation and management.
    /// </summary>
    /// <param name="templateId">The template identifier.</param>
    /// <param name="currentApplication">Optional application used to resolve the template version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> IsEnabledAsync(
        string templateId,
        ApplicationDto? currentApplication = null,
        CancellationToken cancellationToken = default);
}
