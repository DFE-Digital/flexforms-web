using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;

namespace GovUK.Dfe.FlexForms.Application.Interfaces;

/// <summary>
/// Service for managing contributors to applications
/// </summary>
public interface IContributorService
{
    /// <summary>
    /// Gets all contributors for a specific application
    /// </summary>
    /// <param name="applicationId">The application ID to get contributors for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of contributors for the application</returns>
    Task<IReadOnlyList<UserDto>> GetApplicationContributorsAsync(Guid applicationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invites a new contributor to an application
    /// </summary>
    /// <param name="applicationId">The application ID to invite contributor to</param>
    /// <param name="request">The invitation request containing email address</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task indicating completion</returns>
    System.Threading.Tasks.Task InviteContributorAsync(Guid applicationId, AddContributorRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a contributor from an application
    /// </summary>
    /// <param name="applicationId">The application ID to remove contributor from</param>
    /// <param name="contributorId">The contributor ID to remove</param>s
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task indicating completion</returns>
    System.Threading.Tasks.Task RemoveContributorAsync(Guid applicationId, Guid contributorId, CancellationToken cancellationToken = default);
} 