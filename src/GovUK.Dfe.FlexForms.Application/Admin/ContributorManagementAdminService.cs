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
            state.TemplateId = application.TemplateSchema?.TemplateId is Guid templateId && templateId != Guid.Empty
                ? templateId
                : null;

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
        state.PageSize = ContributorManagementWorkState.EmailLookupPageSize;
        state.CurrentPage = Math.Max(1, state.CurrentPage);

        try
        {
            var lookup = await usersClient.GetCreatedApplicationsByEmailAsync(
                state.Email,
                cancellationToken);

            var created = (lookup.Applications ?? [])
                .OrderByDescending(a => a.DateCreated)
                .Select(application => new CreatedApplicationInviteSummary
                {
                    ApplicationId = application.ApplicationId,
                    ApplicationReference = application.ApplicationReference,
                    TemplateName = application.TemplateName,
                    Invitees = (application.Invitees ?? [])
                        .OrderBy(i => i.Email)
                        .ThenBy(i => i.Name)
                        .Select(invitee => new UserDto
                        {
                            UserId = invitee.UserId,
                            Name = invitee.Name,
                            Email = invitee.Email
                        })
                        .ToList()
                })
                .ToList();

            state.LookedUpUserId = lookup.UserId;
            state.LookedUpUserName = lookup.Name;
            state.LookedUpUserEmail = string.IsNullOrWhiteSpace(lookup.Email) ? state.Email : lookup.Email;
            state.TotalCount = created.Count;
            state.TotalPages = state.TotalCount == 0
                ? 0
                : (int)Math.Ceiling(state.TotalCount / (double)ContributorManagementWorkState.EmailLookupPageSize);

            if (state.TotalPages > 0 && state.CurrentPage > state.TotalPages)
                state.CurrentPage = state.TotalPages;

            state.CreatedApplications = created
                .Skip((state.CurrentPage - 1) * ContributorManagementWorkState.EmailLookupPageSize)
                .Take(ContributorManagementWorkState.EmailLookupPageSize)
                .ToList();

            await EnrichTemplateIdsAsync(state.CreatedApplications, cancellationToken);
        }
        catch (ExternalApplicationsException ex) when (ex.StatusCode == 400)
        {
            state.HasError = true;
            var message = AdminApiErrorMapper.Format(ex, ContributorManagementMessages.InvalidEmail);
            state.ErrorMessage = message.Contains("Email", StringComparison.OrdinalIgnoreCase)
                && message.Contains("valid", StringComparison.OrdinalIgnoreCase)
                ? ContributorManagementMessages.InvalidEmail
                : message;
            state.CreatedApplications = [];
        }
        catch (ExternalApplicationsException ex) when (ex.StatusCode == 404)
        {
            state.HasError = true;
            state.ErrorMessage = ContributorManagementMessages.UserNotFound;
            state.CreatedApplications = [];
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

    private async Task EnrichTemplateIdsAsync(
        IReadOnlyList<CreatedApplicationInviteSummary> applications,
        CancellationToken cancellationToken)
    {
        foreach (var application in applications)
        {
            if (application.TemplateId is Guid existing && existing != Guid.Empty)
                continue;

            if (string.IsNullOrWhiteSpace(application.ApplicationReference))
                continue;

            application.TemplateId = await ResolveTemplateIdAsync(application.ApplicationReference, cancellationToken);
        }
    }

    private async Task<Guid?> ResolveTemplateIdAsync(
        string applicationReference,
        CancellationToken cancellationToken)
    {
        try
        {
            var application = await applicationsClient.GetApplicationByReferenceAsync(
                applicationReference,
                cancellationToken);

            return application.TemplateSchema?.TemplateId is Guid templateId && templateId != Guid.Empty
                ? templateId
                : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not resolve template for {ApplicationReference}", applicationReference);
            return null;
        }
    }
}
