using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Lists tenant users and removes membership for the current tenant.
/// </summary>
public interface IUserManagerAdmin
{
    Task LoadAsync(UserManagerWorkState state, CancellationToken cancellationToken = default);

    Task<AdminPageOutcome> RemoveAsync(
        UserManagerWorkState state,
        Guid userId,
        CancellationToken cancellationToken = default);
}

public sealed class UserManagerAdminService(
    IUsersClient usersClient,
    ILogger<UserManagerAdminService> logger) : IUserManagerAdmin
{
    public async Task LoadAsync(UserManagerWorkState state, CancellationToken cancellationToken = default)
    {
        await LoadUsersAsync(state, cancellationToken);
        await LoadAccessAuditLogAsync(state, cancellationToken);
    }

    public async Task<AdminPageOutcome> RemoveAsync(
        UserManagerWorkState state,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await usersClient.RemoveUserFromTenantAsync(userId, cancellationToken);
            return AdminPageOutcome.Redirect(successMessage: UserManagerMessages.Removed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove user {UserId} from tenant", userId);
            return AdminPageOutcome.Redirect(
                errorMessage: AdminApiErrorMapper.Format(ex, UserManagerMessages.RemoveFailed));
        }
    }

    private async Task LoadUsersAsync(UserManagerWorkState state, CancellationToken cancellationToken)
    {
        try
        {
            var users = await usersClient.GetTenantUsersAsync(cancellationToken);
            state.Users = users?.OrderBy(u => u.Name).ToList() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load tenant users");
            state.HasError = true;
            state.ErrorMessage = AdminApiErrorMapper.Format(ex, UserManagerMessages.LoadFailed);
            state.Users = [];
        }
    }

    private async Task LoadAccessAuditLogAsync(UserManagerWorkState state, CancellationToken cancellationToken)
    {
        try
        {
            var log = await usersClient.GetAccessAuditLogAsync(take: 50, cancellationToken);
            state.AccessAuditEntries = log?.Entries?
                .OrderByDescending(e => e.OccurredAtUtc)
                .ToList() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load tenant access audit log");
            state.AccessAuditEntries = [];
            state.AuditLogLoadFailed = true;
            state.AuditLogLoadErrorMessage = AdminApiErrorMapper.Format(
                ex,
                UserManagerMessages.AuditLogLoadFailed);
        }
    }
}
