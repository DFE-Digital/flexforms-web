using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Looks up application contributors by reference number, or applications a user created and who they invited.
/// </summary>
public interface IContributorManagementAdmin
{
    Task LookupAsync(ContributorManagementWorkState state, CancellationToken cancellationToken = default);

    Task LookupByEmailAsync(ContributorManagementWorkState state, CancellationToken cancellationToken = default);
}

public sealed class ContributorManagementAdminService(
    IApplicationsClient applicationsClient,
    IUsersClient usersClient,
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

    public async Task LookupByEmailAsync(
        ContributorManagementWorkState state,
        CancellationToken cancellationToken = default)
    {
        state.EmailLookupPerformed = true;
        state.LookedUpUserEmail = state.Email;

        try
        {
            var tenantUsers = await usersClient.GetTenantUsersAsync(cancellationToken);
            var tenantUser = tenantUsers?
                .FirstOrDefault(u => string.Equals(u.Email, state.Email, StringComparison.OrdinalIgnoreCase));

            if (tenantUser is null)
            {
                state.HasError = true;
                state.ErrorMessage = ContributorManagementMessages.UserNotFound;
                state.CreatedApplications = [];
                return;
            }

            var applications = await applicationsClient.GetApplicationsForUserAsync(
                state.Email,
                includeSchema: false,
                templateId: null,
                cancellationToken);

            var created = new List<CreatedApplicationInviteSummary>();
            foreach (var listing in applications.Items ?? Array.Empty<ApplicationDto>())
            {
                var detail = await applicationsClient.GetApplicationByReferenceAsync(
                    listing.ApplicationReference,
                    cancellationToken);

                if (detail.CreatedBy?.UserId != tenantUser.UserId)
                    continue;

                var contributors = await applicationsClient.GetContributorsAsync(
                    detail.ApplicationId,
                    includePermissionDetails: false,
                    cancellationToken);

                created.Add(new CreatedApplicationInviteSummary
                {
                    ApplicationId = detail.ApplicationId,
                    ApplicationReference = string.IsNullOrWhiteSpace(detail.ApplicationReference)
                        ? listing.ApplicationReference
                        : detail.ApplicationReference,
                    TemplateName = detail.TemplateName,
                    Invitees = contributors?
                        .OrderBy(c => c.Email)
                        .ThenBy(c => c.Name)
                        .ToList() ?? []
                });
            }

            state.LookedUpUserId = tenantUser.UserId;
            state.LookedUpUserName = tenantUser.Name;
            state.LookedUpUserEmail = string.IsNullOrWhiteSpace(tenantUser.Email) ? state.Email : tenantUser.Email;
            state.CreatedApplications = created
                .OrderBy(a => a.ApplicationReference)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to look up applications created by {Email}", state.Email);
            state.HasError = true;
            state.ErrorMessage = AdminApiErrorMapper.Format(
                ex,
                ContributorManagementMessages.EmailLookupFailed,
                includeGatewayHint: false);
            state.CreatedApplications = [];
        }
    }
}
