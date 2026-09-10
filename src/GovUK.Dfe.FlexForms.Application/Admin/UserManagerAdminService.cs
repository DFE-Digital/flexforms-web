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
    IRolesClient rolesClient,
    ILogger<UserManagerAdminService> logger) : IUserManagerAdmin
{
    public async Task LoadAsync(UserManagerWorkState state, CancellationToken cancellationToken = default)
    {
        await LoadFilterRolesAsync(state, cancellationToken);
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

    /// <summary>
    /// Loads tenant roles from the existing roles catalogue used by Add/Edit.
    /// A selected role is always kept in the list so the dropdown can render the current selection.
    /// </summary>
    private async Task LoadFilterRolesAsync(UserManagerWorkState state, CancellationToken cancellationToken)
    {
        var roles = new List<string>();

        try
        {
            var tenantRoles = await rolesClient.ListAsync(cancellationToken);
            if (tenantRoles is not null)
            {
                roles.AddRange(tenantRoles
                    .Select(r => r.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name)));
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load tenant roles for the user filter");
        }

        if (!string.IsNullOrWhiteSpace(state.Role)
            && !roles.Contains(state.Role, StringComparer.OrdinalIgnoreCase))
        {
            roles.Add(state.Role);
        }

        state.AvailableRoles = roles
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task LoadUsersAsync(UserManagerWorkState state, CancellationToken cancellationToken)
    {
        try
        {
            var page = await usersClient.GetTenantUsersAsync(
                pageNumber: Math.Max(1, state.CurrentPage),
                pageSize: UserManagerWorkState.PageSize,
                userId: null,
                email: null,
                searchTerm: NullIfBlank(state.SearchTerm),
                role: NullIfBlank(state.Role),
                cancellationToken);

            state.Users = page?.Items?.ToList() ?? [];
            state.TotalCount = page?.TotalCount ?? 0;
            state.TotalPages = page?.TotalPages ?? 0;
            state.CurrentPage = page?.PageNumber > 0 ? page.PageNumber : 1;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load tenant users");
            state.HasError = true;
            state.ErrorMessage = AdminApiErrorMapper.Format(ex, UserManagerMessages.LoadFailed);
            state.Users = [];
            state.TotalCount = 0;
            state.TotalPages = 0;
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

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
