using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services;

/// <summary>
/// Service for managing contributors to applications via external API client
/// </summary>
[ExcludeFromCodeCoverage]
public class ContributorService(
    IApplicationsClient applicationsClient,
    ILogger<ContributorService> logger) : IContributorService
{
    /// <summary>
    /// Gets all contributors for a specific application
    /// </summary>
    public async Task<IReadOnlyList<UserDto>> GetApplicationContributorsAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Getting contributors for application {ApplicationId}", applicationId);

            var users = await applicationsClient.GetContributorsAsync(applicationId, includePermissionDetails: false, cancellationToken);
            
            return users.AsReadOnly();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting contributors for application {ApplicationId}", applicationId);
            return new List<UserDto>().AsReadOnly();
        }
    }

    /// <summary>
    /// Invites a new contributor to an application
    /// </summary>
    public async System.Threading.Tasks.Task InviteContributorAsync(Guid applicationId, AddContributorRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Inviting contributor {Name} ({Email}) to application {ApplicationId}", request.Name, request.Email, applicationId);

            var user = await applicationsClient.AddContributorAsync(applicationId, request, cancellationToken);
            
            logger.LogInformation("Successfully invited contributor {Name} ({Email}) to application {ApplicationId}", 
                request.Name, request.Email, applicationId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error inviting contributor {Email} to application {ApplicationId}", request.Email, applicationId);
            throw;
        }
    }

    /// <summary>
    /// Removes a contributor from an application
    /// </summary>
    public async System.Threading.Tasks.Task RemoveContributorAsync(Guid applicationId, Guid contributorId, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Removing contributor {ContributorId} from application {ApplicationId}", contributorId, applicationId);

            // The API method expects userId parameter
            await applicationsClient.RemoveContributorAsync(applicationId, contributorId, cancellationToken);
            
            logger.LogInformation("Successfully removed contributor {ContributorId} from application {ApplicationId}", 
                contributorId, applicationId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing contributor {ContributorId} from application {ApplicationId}", contributorId, applicationId);
            throw;
        }
    }
} 