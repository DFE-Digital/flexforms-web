using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Validation;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Lists and creates tenant roles.
/// </summary>
public interface IRoleManagerAdmin
{
    Task LoadAsync(RoleManagerWorkState state, CancellationToken cancellationToken = default);

    Task<AdminPageOutcome> CreateAsync(RoleManagerWorkState state, CancellationToken cancellationToken = default);

    Task<AdminPageOutcome> CreateFromTemplateAsync(
        RoleManagerWorkState state,
        string? templateKey,
        CancellationToken cancellationToken = default);

    Task<AdminPageOutcome> DeleteAsync(
        RoleManagerWorkState state,
        Guid roleId,
        CancellationToken cancellationToken = default);

    Task<AdminPageOutcome> RenameAsync(
        RoleManagerWorkState state,
        Guid roleId,
        string? name,
        CancellationToken cancellationToken = default);
}

public sealed class RoleManagerAdminService(
    IRolesClient rolesClient,
    ILogger<RoleManagerAdminService> logger) : IRoleManagerAdmin
{
    public async Task LoadAsync(RoleManagerWorkState state, CancellationToken cancellationToken = default)
    {
        try
        {
            var roles = await rolesClient.ListAsync(cancellationToken);
            state.Roles = roles?
                .OrderBy(r => r.IsSystem ? 0 : 1)
                .ThenBy(r => r.Name)
                .ToList() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load tenant roles");
            state.HasError = true;
            state.ErrorMessage = AdminApiErrorMapper.Format(ex, RoleManagerMessages.LoadFailed);
            state.Roles = [];
        }
    }

    public async Task<AdminPageOutcome> CreateAsync(
        RoleManagerWorkState state,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var created = await rolesClient.CreateAsync(
                new CreateTenantRoleRequest { Name = state.NewRoleName.Trim() },
                cancellationToken);

            return AdminPageOutcome.Redirect(successMessage: RoleManagerMessages.Created(created.Name));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create role {Name}", state.NewRoleName);
            return AdminPageOutcome.Stay(errors:
            [
                new FormValidationError(
                    string.Empty,
                    AdminApiErrorMapper.Format(ex, RoleManagerMessages.CreateFailed))
            ]);
        }
    }

    public async Task<AdminPageOutcome> CreateFromTemplateAsync(
        RoleManagerWorkState state,
        string? templateKey,
        CancellationToken cancellationToken = default)
    {
        templateKey = templateKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(templateKey))
            return AdminPageOutcome.Redirect(errorMessage: RoleManagerMessages.TemplateRequired);

        try
        {
            var created = await rolesClient.CreateFromTemplateAsync(
                new CreateTenantRoleFromTemplateRequest(templateKey),
                cancellationToken);
            return AdminPageOutcome.Redirect(
                successMessage: RoleManagerMessages.CreatedFromTemplate(created.Name, templateKey));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create role from template {TemplateKey}", templateKey);
            return AdminPageOutcome.Redirect(
                errorMessage: AdminApiErrorMapper.Format(ex, RoleManagerMessages.CreateFromTemplateFailed));
        }
    }

    public async Task<AdminPageOutcome> DeleteAsync(
        RoleManagerWorkState state,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await rolesClient.DeleteAsync(roleId, cancellationToken);
            return AdminPageOutcome.Redirect(successMessage: RoleManagerMessages.Deleted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete role {RoleId}", roleId);
            return AdminPageOutcome.Redirect(
                errorMessage: AdminApiErrorMapper.Format(ex, RoleManagerMessages.DeleteFailed));
        }
    }

    public async Task<AdminPageOutcome> RenameAsync(
        RoleManagerWorkState state,
        Guid roleId,
        string? name,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return AdminPageOutcome.Redirect(errorMessage: RoleManagerMessages.NameRequired);

        try
        {
            await rolesClient.RenameAsync(
                roleId,
                new RenameTenantRoleRequest { Name = name.Trim() },
                cancellationToken);
            return AdminPageOutcome.Redirect(successMessage: RoleManagerMessages.Renamed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to rename role {RoleId}", roleId);
            return AdminPageOutcome.Redirect(
                errorMessage: AdminApiErrorMapper.Format(ex, RoleManagerMessages.RenameFailed));
        }
    }
}
