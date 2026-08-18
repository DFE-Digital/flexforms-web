using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Validation;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Sets RolePermissions for a custom tenant role (ResourceType + ResourceKey + AccessType).
/// </summary>
public interface IRoleManagerPermissionsAdmin
{
    Task<AdminPageOutcome> LoadAsync(
        RoleManagerPermissionsWorkState state,
        CancellationToken cancellationToken = default);

    Task<AdminPageOutcome> AddGrantAsync(
        RoleManagerPermissionsWorkState state,
        CancellationToken cancellationToken = default);

    Task<AdminPageOutcome> RemoveGrantAsync(
        RoleManagerPermissionsWorkState state,
        string grantKey,
        CancellationToken cancellationToken = default);
}

public sealed class RoleManagerPermissionsAdminService(
    IRolesClient rolesClient,
    ILogger<RoleManagerPermissionsAdminService> logger) : IRoleManagerPermissionsAdmin
{
    public async Task<AdminPageOutcome> LoadAsync(
        RoleManagerPermissionsWorkState state,
        CancellationToken cancellationToken = default)
    {
        if (await LoadRoleMetaAsync(state, cancellationToken) is { } failure)
            return failure;

        if (state.IsSystemRole)
        {
            return AdminPageOutcome.Redirect(
                errorMessage: RoleManagerPermissionsMessages.SystemRoleCannotChangeCreateCustom);
        }

        try
        {
            await LoadPermissionsAsync(state, cancellationToken);
            return AdminPageOutcome.Stay();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load permissions for role {RoleId}", state.RoleId);
            return AdminPageOutcome.Redirect(
                errorMessage: AdminApiErrorMapper.Format(ex, RoleManagerPermissionsMessages.LoadPermissionsFailed));
        }
    }

    public async Task<AdminPageOutcome> AddGrantAsync(
        RoleManagerPermissionsWorkState state,
        CancellationToken cancellationToken = default)
    {
        if (await LoadRoleMetaAsync(state, cancellationToken) is { } failure)
            return failure;

        if (state.IsSystemRole)
            return AdminPageOutcome.Redirect(errorMessage: RoleManagerPermissionsMessages.SystemRoleCannotChange);

        state.SelectedGrants = AdminPermissionGrants.NormalizeGrants(state.SelectedGrants);

        var resourceKey = state.NewResourceKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            return AdminPageOutcome.Stay(errors:
            [
                new FormValidationError(
                    nameof(RoleManagerPermissionsWorkState.NewResourceKey),
                    RoleManagerPermissionsMessages.ResourceKeyRequired)
            ]);
        }

        var validationError = AdminPermissionGrants.ValidateGrant(
            state.NewResourceType,
            resourceKey,
            state.NewAccessType);
        if (validationError is not null)
        {
            return AdminPageOutcome.Stay(errors:
            [
                new FormValidationError(nameof(RoleManagerPermissionsWorkState.NewResourceKey), validationError)
            ]);
        }

        var key = AdminPermissionGrants.EncodeGrantKey(state.NewResourceType, resourceKey, state.NewAccessType);
        if (state.SelectedGrants.Contains(key, StringComparer.OrdinalIgnoreCase))
        {
            return AdminPageOutcome.Stay(errors:
            [
                new FormValidationError(
                    string.Empty,
                    RoleManagerPermissionsMessages.DuplicateGrant(
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
        RoleManagerPermissionsWorkState state,
        string grantKey,
        CancellationToken cancellationToken = default)
    {
        if (await LoadRoleMetaAsync(state, cancellationToken) is { } failure)
            return failure;

        if (state.IsSystemRole)
            return AdminPageOutcome.Redirect(errorMessage: RoleManagerPermissionsMessages.SystemRoleCannotChange);

        state.SelectedGrants = AdminPermissionGrants.NormalizeGrants(state.SelectedGrants);
        state.SelectedGrants.RemoveAll(g => string.Equals(g, grantKey, StringComparison.OrdinalIgnoreCase));

        return await SaveAndReloadAsync(state, cancellationToken);
    }

    private async Task<AdminPageOutcome> SaveAndReloadAsync(
        RoleManagerPermissionsWorkState state,
        CancellationToken cancellationToken)
    {
        foreach (var grant in state.SelectedGrants.Select(AdminPermissionGrants.ParseGrantKey).Where(g => g is not null))
        {
            var error = AdminPermissionGrants.ValidateGrant(
                grant!.Value.ResourceType,
                grant.Value.ResourceKey,
                grant.Value.AccessType);
            if (error is not null)
                return AdminPageOutcome.Stay(errors: [new FormValidationError(string.Empty, error)]);
        }

        try
        {
            var grants = AdminPermissionGrants.ToGrantDtos(state.SelectedGrants);

            await rolesClient.SetPermissionsAsync(
                state.RoleId,
                new SetRolePermissionsRequest { Permissions = grants },
                cancellationToken);

            state.NewResourceKey = string.Empty;
            await LoadPermissionsAsync(state, cancellationToken);
            return AdminPageOutcome.Stay();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set permissions for role {RoleId}", state.RoleId);
            await LoadPermissionsAsync(state, cancellationToken);

            return AdminPageOutcome.Stay(errors:
            [
                new FormValidationError(
                    string.Empty,
                    AdminApiErrorMapper.Format(ex, RoleManagerPermissionsMessages.SaveFailed))
            ]);
        }
    }

    private async Task LoadPermissionsAsync(
        RoleManagerPermissionsWorkState state,
        CancellationToken cancellationToken)
    {
        var existing = await rolesClient.GetPermissionsAsync(state.RoleId, cancellationToken);
        state.SelectedGrants = AdminPermissionGrants.NormalizeGrants(
            existing?
                .Select(p => AdminPermissionGrants.EncodeGrantKey(p.ResourceType, p.ResourceKey, p.AccessType))
                .ToList() ?? []);
    }

    /// <returns>A redirect outcome when the role cannot be loaded; otherwise null.</returns>
    private async Task<AdminPageOutcome?> LoadRoleMetaAsync(
        RoleManagerPermissionsWorkState state,
        CancellationToken cancellationToken)
    {
        try
        {
            var roles = await rolesClient.ListAsync(cancellationToken);
            var role = roles?.FirstOrDefault(r => r.RoleId == state.RoleId);
            if (role is null)
                return AdminPageOutcome.Redirect(errorMessage: RoleManagerPermissionsMessages.RoleNotFound);

            state.RoleName = role.Name;
            state.IsSystemRole = role.IsSystem;
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load role {RoleId}", state.RoleId);
            return AdminPageOutcome.Redirect(
                errorMessage: AdminApiErrorMapper.Format(ex, RoleManagerPermissionsMessages.LoadRoleFailed));
        }
    }
}
