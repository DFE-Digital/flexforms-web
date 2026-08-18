using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Validation;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Registers a user into the tenant with a role and optional form access.
/// </summary>
public interface IUserManagerAddAdmin
{
    Task LoadAsync(UserManagerAddWorkState state, CancellationToken cancellationToken = default);

    Task<AdminPageOutcome> AddAsync(UserManagerAddWorkState state, CancellationToken cancellationToken = default);
}

public sealed class UserManagerAddAdminService(
    IUsersClient usersClient,
    ITemplatesClient templatesClient,
    IRolesClient rolesClient,
    ILogger<UserManagerAddAdminService> logger) : IUserManagerAddAdmin
{
    public async Task LoadAsync(UserManagerAddWorkState state, CancellationToken cancellationToken = default)
    {
        var errors = new List<FormValidationError>();
        await LoadTemplatesAsync(state, errors, cancellationToken);
        await LoadRolesAsync(state, errors, cancellationToken);
        state.Errors = errors;
    }

    public async Task<AdminPageOutcome> AddAsync(
        UserManagerAddWorkState state,
        CancellationToken cancellationToken = default)
    {
        if (!state.AssignableRoles.Contains(state.Role, StringComparer.OrdinalIgnoreCase))
        {
            return Stay(state, new FormValidationError(
                nameof(UserManagerAddWorkState.Role),
                UserManagerAddMessages.InvalidRole));
        }

        var isSystemUserRole = string.Equals(state.Role, "User", StringComparison.OrdinalIgnoreCase);
        if (isSystemUserRole && (state.SelectedTemplateIds is null || state.SelectedTemplateIds.Count == 0))
        {
            return Stay(state, new FormValidationError(
                nameof(UserManagerAddWorkState.SelectedTemplateIds),
                UserManagerAddMessages.UserRoleRequiresTemplate));
        }

        try
        {
            var existingUsers = await usersClient.GetTenantUsersAsync(cancellationToken);
            if (existingUsers?.Any(u =>
                    string.Equals(u.Email, state.Email.Trim(), StringComparison.OrdinalIgnoreCase)) == true)
            {
                return Stay(state, new FormValidationError(
                    nameof(UserManagerAddWorkState.Email),
                    UserManagerAddMessages.DuplicateEmail));
            }

            var created = await usersClient.AssignUserRoleAsync(
                new AssignUserRoleRequest
                {
                    Name = state.Name.Trim(),
                    Email = state.Email.Trim(),
                    Role = state.Role,
                    TemplateIds = state.SelectedTemplateIds
                },
                createOnly: true,
                cancellationToken);

            if (created?.UserId is Guid userId && state.SelectedTemplateIds is { Count: > 0 })
            {
                await usersClient.UpdateUserTemplateAccessAsync(
                    userId,
                    new UpdateUserTemplateAccessRequest { TemplateIds = state.SelectedTemplateIds },
                    cancellationToken);
            }

            return AdminPageOutcome.Redirect(
                successMessage: UserManagerAddMessages.Added(state.Email.Trim(), state.Role));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add user {Email}", state.Email);
            return Stay(state, new FormValidationError(
                string.Empty,
                AdminApiErrorMapper.Format(ex, UserManagerAddMessages.AddFailed)));
        }
    }

    private async Task LoadTemplatesAsync(
        UserManagerAddWorkState state,
        List<FormValidationError> errors,
        CancellationToken cancellationToken)
    {
        try
        {
            var templates = await templatesClient.GetAccessibleTemplatesAsync(cancellationToken);
            state.AvailableTemplates = templates?.OrderBy(t => t.Name).ToList() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load templates for add user");
            errors.Add(new FormValidationError(
                string.Empty,
                AdminApiErrorMapper.Format(ex, UserManagerAddMessages.LoadTemplatesFailed)));
            state.AvailableTemplates = [];
        }
    }

    private async Task LoadRolesAsync(
        UserManagerAddWorkState state,
        List<FormValidationError> errors,
        CancellationToken cancellationToken)
    {
        try
        {
            var roles = await rolesClient.ListAsync(cancellationToken);
            state.AssignableRoles = UserManagerAssignableRoles.Resolve(
                roles?.Select(r => (r.Name, r.IsSystem)),
                state.IncludeTenantAdmin);

            if (string.IsNullOrWhiteSpace(state.Role)
                || !state.AssignableRoles.Contains(state.Role, StringComparer.OrdinalIgnoreCase))
            {
                state.Role = state.AssignableRoles.FirstOrDefault() ?? "User";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load roles for add user");
            errors.Add(new FormValidationError(
                string.Empty,
                AdminApiErrorMapper.Format(ex, UserManagerAddMessages.LoadRolesFailed)));
            state.AssignableRoles = UserManagerAssignableRoles.Resolve(null, state.IncludeTenantAdmin);
        }
    }

    private static AdminPageOutcome Stay(UserManagerAddWorkState state, FormValidationError error)
    {
        state.Errors = [error];
        return AdminPageOutcome.Stay(errors: [error]);
    }
}
