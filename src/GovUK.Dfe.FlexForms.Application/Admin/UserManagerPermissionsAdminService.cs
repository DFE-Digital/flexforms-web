using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Validation;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Sets user-level Permissions for a tenant member (ResourceType + ResourceKey + AccessType).
/// Does not affect permissions inherited from the user's role.
/// </summary>
public interface IUserManagerPermissionsAdmin
{
    Task<AdminPageOutcome> LoadAsync(
        UserManagerPermissionsWorkState state,
        CancellationToken cancellationToken = default);

    Task<AdminPageOutcome> AddGrantAsync(
        UserManagerPermissionsWorkState state,
        CancellationToken cancellationToken = default);

    Task<AdminPageOutcome> RemoveGrantAsync(
        UserManagerPermissionsWorkState state,
        string grantKey,
        CancellationToken cancellationToken = default);
}

public sealed class UserManagerPermissionsAdminService(
    IUsersClient usersClient,
    ILogger<UserManagerPermissionsAdminService> logger) : IUserManagerPermissionsAdmin
{
    public async Task<AdminPageOutcome> LoadAsync(
        UserManagerPermissionsWorkState state,
        CancellationToken cancellationToken = default)
    {
        if (await LoadUserMetaAsync(state, cancellationToken) is { } failure)
            return failure;

        try
        {
            await LoadPermissionsAsync(state, cancellationToken);
            return AdminPageOutcome.Stay();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load permissions for user {UserId}", state.UserId);
            return AdminPageOutcome.Redirect(
                errorMessage: AdminApiErrorMapper.Format(ex, UserManagerPermissionsMessages.LoadPermissionsFailed));
        }
    }

    public async Task<AdminPageOutcome> AddGrantAsync(
        UserManagerPermissionsWorkState state,
        CancellationToken cancellationToken = default)
    {
        if (await LoadUserMetaAsync(state, cancellationToken) is { } failure)
            return failure;

        state.SelectedGrants = AdminPermissionGrants.NormalizeGrants(state.SelectedGrants);

        var resourceKey = state.NewResourceKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            return AdminPageOutcome.Stay(errors:
            [
                new FormValidationError(
                    nameof(UserManagerPermissionsWorkState.NewResourceKey),
                    UserManagerPermissionsMessages.ResourceKeyRequired)
            ]);
        }

        var validationError = AdminPermissionGrants.ValidateUserGrant(
            state.NewResourceType,
            resourceKey,
            state.NewAccessType);
        if (validationError is not null)
        {
            return AdminPageOutcome.Stay(errors:
            [
                new FormValidationError(nameof(UserManagerPermissionsWorkState.NewResourceKey), validationError)
            ]);
        }

        var key = AdminPermissionGrants.EncodeGrantKey(state.NewResourceType, resourceKey, state.NewAccessType);
        if (state.SelectedGrants.Contains(key, StringComparer.OrdinalIgnoreCase))
        {
            return AdminPageOutcome.Stay(errors:
            [
                new FormValidationError(
                    string.Empty,
                    UserManagerPermissionsMessages.DuplicateGrant(
                        state.NewResourceType.ToString(),
                        resourceKey,
                        state.NewAccessType.ToString()))
            ]);
        }

        state.SelectedGrants.Add(key);
        state.SelectedGrants = AdminPermissionGrants.NormalizeGrants(state.SelectedGrants);

        return await SaveAndReloadAsync(state, cancellationToken);
    }

    public async Task<AdminPageOutcome> RemoveGrantAsync(
        UserManagerPermissionsWorkState state,
        string grantKey,
        CancellationToken cancellationToken = default)
    {
        if (await LoadUserMetaAsync(state, cancellationToken) is { } failure)
            return failure;

        state.SelectedGrants = AdminPermissionGrants.NormalizeGrants(state.SelectedGrants);
        state.SelectedGrants.RemoveAll(g => string.Equals(g, grantKey, StringComparison.OrdinalIgnoreCase));

        return await SaveAndReloadAsync(state, cancellationToken);
    }

    private async Task<AdminPageOutcome> SaveAndReloadAsync(
        UserManagerPermissionsWorkState state,
        CancellationToken cancellationToken)
    {
        foreach (var grant in state.SelectedGrants.Select(AdminPermissionGrants.ParseGrantKey).Where(g => g is not null))
        {
            var error = AdminPermissionGrants.ValidateUserGrant(
                grant!.Value.ResourceType,
                grant.Value.ResourceKey,
                grant.Value.AccessType);
            if (error is not null)
                return AdminPageOutcome.Stay(errors: [new FormValidationError(string.Empty, error)]);
        }

        try
        {
            var grants = AdminPermissionGrants.ToGrantDtos(state.SelectedGrants);

            await usersClient.SetUserPermissionsAsync(
                state.UserId,
                new SetUserPermissionsRequest { Permissions = grants },
                cancellationToken);

            state.NewResourceKey = string.Empty;
            await LoadPermissionsAsync(state, cancellationToken);
            return AdminPageOutcome.Stay();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save permissions for user {UserId}", state.UserId);
            await LoadPermissionsAsync(state, cancellationToken);

            return AdminPageOutcome.Stay(errors:
            [
                new FormValidationError(
                    string.Empty,
                    AdminApiErrorMapper.Format(ex, UserManagerPermissionsMessages.SaveFailed))
            ]);
        }
    }

    private async Task LoadPermissionsAsync(
        UserManagerPermissionsWorkState state,
        CancellationToken cancellationToken)
    {
        var existing = await usersClient.GetUserPermissionsAsync(state.UserId, cancellationToken);
        state.SelectedGrants = AdminPermissionGrants.NormalizeGrants(
            existing?
                .Select(p => AdminPermissionGrants.EncodeGrantKey(p.ResourceType, p.ResourceKey, p.AccessType))
                .ToList() ?? []);
    }

    /// <returns>A redirect outcome when the user cannot be loaded; otherwise null.</returns>
    private async Task<AdminPageOutcome?> LoadUserMetaAsync(
        UserManagerPermissionsWorkState state,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await TenantUserDirectory.GetByIdAsync(usersClient, state.UserId, cancellationToken);
            if (user is null)
                return AdminPageOutcome.Redirect(errorMessage: UserManagerPermissionsMessages.UserNotFound);

            state.UserName = user.Name;
            state.UserEmail = user.Email;
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load user {UserId}", state.UserId);
            return AdminPageOutcome.Redirect(
                errorMessage: AdminApiErrorMapper.Format(ex, UserManagerPermissionsMessages.LoadUserFailed));
        }
    }
}
