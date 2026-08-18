using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Looks up application contributors by reference number.
/// </summary>
public interface IContributorManagementAdmin
{
    Task LookupAsync(ContributorManagementWorkState state, CancellationToken cancellationToken = default);
}

public sealed class ContributorManagementAdminService(
    IApplicationsClient applicationsClient,
    ILogger<ContributorManagementAdminService> logger) : IContributorManagementAdmin
{
    public async Task LookupAsync(
        ContributorManagementWorkState state,
        CancellationToken cancellationToken = default)
    {
        state.LookupPerformed = true;
        state.ApplicationReference = state.ReferenceNumber;

        try
        {
            var application = await applicationsClient.GetApplicationByReferenceAsync(
                state.ReferenceNumber,
                cancellationToken);

            state.ApplicationId = application.ApplicationId;
            state.ApplicationReference = string.IsNullOrWhiteSpace(application.ApplicationReference)
                ? state.ReferenceNumber
                : application.ApplicationReference;
            state.TemplateName = application.TemplateName;

            var contributors = await applicationsClient.GetContributorsAsync(
                application.ApplicationId,
                includePermissionDetails: false,
                cancellationToken);

            state.Contributors = contributors?
                .OrderBy(c => c.Name)
                .ThenBy(c => c.Email)
                .ToList() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to look up contributors for {ReferenceNumber}", state.ReferenceNumber);
            state.HasError = true;
            state.ErrorMessage = AdminApiErrorMapper.Format(
                ex,
                ContributorManagementMessages.LookupFailed,
                includeGatewayHint: false);
            state.Contributors = [];
        }
    }
}
