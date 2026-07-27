using System.ComponentModel.DataAnnotations;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Edits a tenant user's role and form access.
/// </summary>
[Authorize(Roles = AdminAccessHelper.AuthorizeRoles)]
public sealed class UserManagerEditModel(
    IUsersClient usersClient,
    ITemplatesClient templatesClient,
    IRolesClient rolesClient,
    ILogger<UserManagerEditModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid UserId { get; set; }

    public string UserName { get; private set; } = string.Empty;

    public string UserEmail { get; private set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Select a role")]
    public string Role { get; set; } = string.Empty;

    public IReadOnlyList<TemplateDto> AvailableTemplates { get; private set; } = [];

    public IReadOnlyList<string> AssignableRoles { get; private set; } = [];

    [BindProperty]
    public List<Guid> SelectedTemplateIds { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(cancellationToken);
        return loaded ? Page() : RedirectToPage("/Admin/UserManager");
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await LoadLookupsAsync(cancellationToken);

        var users = await usersClient.GetTenantUsersAsync(cancellationToken);
        var user = users?.FirstOrDefault(u => u.UserId == UserId);
        if (user is null)
        {
            TempData["UserManagerError"] = "User not found in this tenant.";
            return RedirectToPage("/Admin/UserManager");
        }

        UserName = user.Name;
        UserEmail = user.Email;

        if (!ModelState.IsValid)
            return Page();

        if (!AssignableRoles.Contains(Role, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(Role), "Select a valid role for this tenant.");
            return Page();
        }

        try
        {
            await usersClient.AssignUserRoleAsync(
                new AssignUserRoleRequest
                {
                    Name = UserName,
                    Email = UserEmail,
                    Role = Role,
                    TemplateIds = SelectedTemplateIds
                },
                cancellationToken);

            await usersClient.UpdateUserTemplateAccessAsync(
                UserId,
                new UpdateUserTemplateAccessRequest { TemplateIds = SelectedTemplateIds ?? [] },
                cancellationToken);

            TempData["UserManagerSuccess"] = "User role and form access updated.";
            return RedirectToPage("/Admin/UserManager");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update user {UserId}", UserId);
            ModelState.AddModelError(string.Empty, UserManagerModel.GetErrorMessage(ex, "Could not update the user."));
            return Page();
        }
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await LoadLookupsAsync(cancellationToken);

            var users = await usersClient.GetTenantUsersAsync(cancellationToken);
            var user = users?.FirstOrDefault(u => u.UserId == UserId);
            if (user is null)
            {
                TempData["UserManagerError"] = "User not found in this tenant.";
                return false;
            }

            UserName = user.Name;
            UserEmail = user.Email;
            Role = user.Role;

            if (!AssignableRoles.Contains(Role, StringComparer.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(Role))
            {
                AssignableRoles = AssignableRoles.Append(Role).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(r => r).ToList();
            }

            if (SelectedTemplateIds.Count == 0)
                SelectedTemplateIds = user.Templates.Select(t => t.TemplateId).ToList();

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load user {UserId} for edit", UserId);
            TempData["UserManagerError"] = UserManagerModel.GetErrorMessage(ex, "Could not load user details.");
            return false;
        }
    }

    private async Task LoadLookupsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var templates = await templatesClient.GetAccessibleTemplatesAsync(cancellationToken);
            AvailableTemplates = templates?.OrderBy(t => t.Name).ToList() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load templates for edit user");
            ModelState.AddModelError(string.Empty, UserManagerModel.GetErrorMessage(ex, "Could not load available forms."));
            AvailableTemplates = [];
        }

        try
        {
            var roles = await rolesClient.ListAsync(cancellationToken);
            AssignableRoles = roles?
                .Where(r =>
                    string.Equals(r.Name, "User", StringComparison.OrdinalIgnoreCase)
                    || !r.IsSystem)
                .Select(r => r.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n)
                .ToList() ?? ["User"];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load roles for edit user");
            ModelState.AddModelError(string.Empty, UserManagerModel.GetErrorMessage(ex, "Could not load available roles."));
            AssignableRoles = ["User"];
        }
    }
}
