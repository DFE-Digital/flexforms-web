using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services;

/// <summary>
/// Reads the contributorPattern flag from the active form template.
/// </summary>
public class ContributorPatternService(ITemplateManagementService templateManagementService)
    : IContributorPatternService
{
    /// <inheritdoc />
    public async Task<bool> IsEnabledAsync(
        string templateId,
        ApplicationDto? currentApplication = null,
        CancellationToken cancellationToken = default)
    {
        var template = await templateManagementService.LoadTemplateAsync(templateId, currentApplication);
        return template.ContributorPattern;
    }
}
