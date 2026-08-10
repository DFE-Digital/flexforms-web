using System.ComponentModel.DataAnnotations;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.CoreLibs.Http.Models;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Lists and creates tenant roles.
/// </summary>
[Authorize(Roles = AdminAccessHelper.AuthorizeRoles)]
public sealed class RoleManagerModel(
    IRolesClient rolesClient,
    ILogger<RoleManagerModel> logger) : PageModel
{
    public IReadOnlyList<TenantRoleDto> Roles { get; private set; } = [];

    public bool HasError { get; private set; }

    public string? ErrorMessage { get; private set; }

    public bool ShowSuccess { get; private set; }

    public string? SuccessMessage { get; private set; }

    [BindProperty]
    [Required(ErrorMessage = "Enter a role name")]
    [StringLength(100, MinimumLength = 2)]
    public string NewRoleName { get; set; } = string.Empty;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        ApplyTempData();
        await LoadRolesAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        await LoadRolesAsync(cancellationToken);

        if (!ModelState.IsValid)
            return Page();

        try
        {
            var created = await rolesClient.CreateAsync(
                new CreateTenantRoleRequest { Name = NewRoleName.Trim() },
                cancellationToken);

            TempData["RoleManagerSuccess"] = $"Role '{created.Name}' has been created.";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create role {Name}", NewRoleName);
            ModelState.AddModelError(string.Empty, GetErrorMessage(ex, "Could not create the role."));
            return Page();
        }
    }

    public async Task<IActionResult> OnPostCreateFromTemplateAsync(
        string templateKey,
        CancellationToken cancellationToken)
    {
        templateKey = templateKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(templateKey))
        {
            TempData["RoleManagerError"] = "Choose a role template.";
            return RedirectToPage();
        }

        try
        {
            var created = await rolesClient.CreateFromTemplateAsync(
                new CreateTenantRoleFromTemplateRequest(templateKey),
                cancellationToken);
            TempData["RoleManagerSuccess"] =
                $"Role '{created.Name}' has been created from the {templateKey} template.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create role from template {TemplateKey}", templateKey);
            TempData["RoleManagerError"] = GetErrorMessage(ex, "Could not create the role from template.");
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid roleId, CancellationToken cancellationToken)
    {
        try
        {
            await rolesClient.DeleteAsync(roleId, cancellationToken);
            TempData["RoleManagerSuccess"] = "Role deleted.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete role {RoleId}", roleId);
            TempData["RoleManagerError"] = GetErrorMessage(ex, "Could not delete the role.");
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRenameAsync(
        Guid roleId,
        string name,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["RoleManagerError"] = "Enter a role name.";
            return RedirectToPage();
        }

        try
        {
            await rolesClient.RenameAsync(
                roleId,
                new RenameTenantRoleRequest { Name = name.Trim() },
                cancellationToken);
            TempData["RoleManagerSuccess"] = "Role renamed.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to rename role {RoleId}", roleId);
            TempData["RoleManagerError"] = GetErrorMessage(ex, "Could not rename the role.");
        }

        return RedirectToPage();
    }

    private void ApplyTempData()
    {
        if (TempData["RoleManagerSuccess"] is string success)
        {
            ShowSuccess = true;
            SuccessMessage = success;
        }

        if (TempData["RoleManagerError"] is string error)
        {
            HasError = true;
            ErrorMessage = error;
        }
    }

    private async Task LoadRolesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var roles = await rolesClient.ListAsync(cancellationToken);
            Roles = roles?
                .OrderBy(r => r.IsSystem ? 0 : 1)
                .ThenBy(r => r.Name)
                .ToList() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load tenant roles");
            HasError = true;
            ErrorMessage = GetErrorMessage(ex, "Could not load roles for this tenant.");
            Roles = [];
        }
    }

    internal static string GetErrorMessage(Exception ex, string fallback)
    {
        if (ex is ExternalApplicationsException<ExceptionResponse> apiEx
            && !string.IsNullOrWhiteSpace(apiEx.Result?.Message))
        {
            return apiEx.Result.Message;
        }

        return fallback;
    }
}
