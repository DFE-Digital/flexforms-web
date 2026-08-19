using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Validation;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Edits a tenant user's role and form access.
/// </summary>
public interface IUserManagerEditAdmin
{
    Task<AdminPageOutcome> LoadAsync(UserManagerEditWorkState state, CancellationToken cancellationToken = default);

    Task<AdminPageOutcome> LoadForUpdateAsync(
        UserManagerEditWorkState state,
        CancellationToken cancellationToken = default);

    Task<AdminPageOutcome> UpdateAsync(UserManagerEditWorkState state, CancellationToken cancellationToken = default);
}

public sealed class UserManagerEditAdminService(
    IUsersClient usersClient,
    ITemplatesClient templatesClient,
    IRolesClient rolesClient,
    ILogger<UserManagerEditAdminService> logger) : IUserManagerEditAdmin
{
    public async Task<AdminPageOutcome> LoadAsync(
        UserManagerEditWorkState state,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var errors = await LoadLookupsAsync(state, cancellationToken);

            var user = await TenantUserDirectory.GetByIdAsync(usersClient, state.UserId, cancellationToken);
            if (user is null)
                return AdminPageOutcome.Redirect(errorMessage: UserManagerEditMessages.UserNotFound);

            state.UserName = user.Name;
            state.UserEmail = user.Email;
            state.Role = user.Role;

            if (!state.AssignableRoles.Contains(state.Role, StringComparer.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(state.Role))
            {
                state.AssignableRoles = state.AssignableRoles
                    .Append(state.Role)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(r => r)
                    .ToList();
            }

            if (state.SelectedTemplateIds.Count == 0)
                state.SelectedTemplateIds = user.Templates.Select(t => t.TemplateId).ToList();

            state.Errors = errors;
            return AdminPageOutcome.Stay(errors: errors);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load user {UserId} for edit", state.UserId);
            return AdminPageOutcome.Redirect(
                errorMessage: AdminApiErrorMapper.Format(ex, UserManagerEditMessages.LoadFailed));
        }
    }

    public async Task<AdminPageOutcome> LoadForUpdateAsync(
        UserManagerEditWorkState state,
        CancellationToken cancellationToken = default)
    {
        var errors = await LoadLookupsAsync(state, cancellationToken);

        var user = await TenantUserDirectory.GetByIdAsync(usersClient, state.UserId, cancellationToken);
        if (user is null)
            return AdminPageOutcome.Redirect(errorMessage: UserManagerEditMessages.UserNotFound);

        state.UserName = user.Name;
        state.UserEmail = user.Email;
        state.Errors = errors;
        return AdminPageOutcome.Stay(errors: errors);
    }

    public async Task<AdminPageOutcome> UpdateAsync(
        UserManagerEditWorkState state,
        CancellationToken cancellationToken = default)
    {
        if (!state.AssignableRoles.Contains(state.Role, StringComparer.OrdinalIgnoreCase))
        {
            var error = new FormValidationError(
                nameof(UserManagerEditWorkState.Role),
                UserManagerEditMessages.InvalidRole);
            state.Errors = [error];
            return AdminPageOutcome.Stay(errors: [error]);
        }

        try
        {
            var current = await TenantUserDirectory.GetByIdAsync(usersClient, state.UserId, cancellationToken);
            if (current is null)
                return AdminPageOutcome.Redirect(errorMessage: UserManagerEditMessages.UserNotFound);

            var roleChanged = !string.Equals(current.Role, state.Role, StringComparison.OrdinalIgnoreCase);
            if (roleChanged)
            {
                await usersClient.AssignUserRoleAsync(
                    new AssignUserRoleRequest
                    {
                        Name = state.UserName,
                        Email = state.UserEmail,
                        Role = state.Role,
                        TemplateIds = state.SelectedTemplateIds
                    },
                    createOnly: false,
                    cancellationToken);
            }

            await usersClient.UpdateUserTemplateAccessAsync(
                state.UserId,
                new UpdateUserTemplateAccessRequest { TemplateIds = state.SelectedTemplateIds ?? [] },
                cancellationToken);

            return AdminPageOutcome.Redirect(successMessage: UserManagerEditMessages.Updated);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update user {UserId}", state.UserId);
            var error = new FormValidationError(
                string.Empty,
                AdminApiErrorMapper.Format(ex, UserManagerEditMessages.UpdateFailed));
            state.Errors = [error];
            return AdminPageOutcome.Stay(errors: [error]);
        }
    }

    private async Task<List<FormValidationError>> LoadLookupsAsync(
        UserManagerEditWorkState state,
        CancellationToken cancellationToken)
    {
        var errors = new List<FormValidationError>();

        try
        {
            var templates = await templatesClient.GetAccessibleTemplatesAsync(cancellationToken);
            state.AvailableTemplates = templates?.OrderBy(t => t.Name).ToList() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load templates for edit user");
            errors.Add(new FormValidationError(
                string.Empty,
                AdminApiErrorMapper.Format(ex, UserManagerEditMessages.LoadTemplatesFailed)));
            state.AvailableTemplates = [];
        }

        try
        {
            var roles = await rolesClient.ListAsync(cancellationToken);
            state.AssignableRoles = UserManagerAssignableRoles.Resolve(
                roles?.Select(r => (r.Name, r.IsSystem)),
                state.IncludeTenantAdmin);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load roles for edit user");
            errors.Add(new FormValidationError(
                string.Empty,
                AdminApiErrorMapper.Format(ex, UserManagerEditMessages.LoadRolesFailed)));
            state.AssignableRoles = UserManagerAssignableRoles.Resolve(null, state.IncludeTenantAdmin);
        }

        return errors;
    }
}
