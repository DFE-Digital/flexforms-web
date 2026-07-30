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
/// Registers a user into the tenant with a role and optional form access.
/// </summary>
[Authorize(Policy = AdminAccessHelper.CanManageUsersPolicy)]
public sealed class UserManagerAddModel(
    IUsersClient usersClient,
    ITemplatesClient templatesClient,
    IRolesClient rolesClient,
    ILogger<UserManagerAddModel> logger) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "Enter the user's name")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Enter the user's email address")]
    [EmailAddress(ErrorMessage = "Enter a valid email address")]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Select a role")]
    public string Role { get; set; } = "User";

    [BindProperty]
    public List<Guid> SelectedTemplateIds { get; set; } = [];

    public IReadOnlyList<TemplateDto> AvailableTemplates { get; private set; } = [];

    public IReadOnlyList<string> AssignableRoles { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadLookupsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await LoadLookupsAsync(cancellationToken);

        if (!ModelState.IsValid)
            return Page();

        if (!AssignableRoles.Contains(Role, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(Role), "Select a valid role for this tenant.");
            return Page();
        }

        var isSystemUserRole = string.Equals(Role, "User", StringComparison.OrdinalIgnoreCase);
        if (isSystemUserRole && (SelectedTemplateIds is null || SelectedTemplateIds.Count == 0))
        {
            ModelState.AddModelError(nameof(SelectedTemplateIds), "Select at least one form for the User role.");
            return Page();
        }

        try
        {
            var created = await usersClient.AssignUserRoleAsync(
                new AssignUserRoleRequest
                {
                    Name = Name.Trim(),
                    Email = Email.Trim(),
                    Role = Role,
                    TemplateIds = SelectedTemplateIds
                },
                cancellationToken);

            if (created?.UserId is Guid userId && SelectedTemplateIds is { Count: > 0 })
            {
                await usersClient.UpdateUserTemplateAccessAsync(
                    userId,
                    new UpdateUserTemplateAccessRequest { TemplateIds = SelectedTemplateIds },
                    cancellationToken);
            }

            TempData["UserManagerSuccess"] = $"User {Email.Trim()} has been added with role {Role}.";
            return RedirectToPage("/Admin/UserManager");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add user {Email}", Email);
            ModelState.AddModelError(string.Empty, UserManagerModel.GetErrorMessage(ex, "Could not add the user."));
            return Page();
        }
    }

    private async Task LoadLookupsAsync(CancellationToken cancellationToken)
    {
        await LoadTemplatesAsync(cancellationToken);
        await LoadRolesAsync(cancellationToken);
    }

    private async Task LoadTemplatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var templates = await templatesClient.GetAccessibleTemplatesAsync(cancellationToken);
            AvailableTemplates = templates?.OrderBy(t => t.Name).ToList() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load templates for add user");
            ModelState.AddModelError(string.Empty, UserManagerModel.GetErrorMessage(ex, "Could not load available forms."));
            AvailableTemplates = [];
        }
    }

    private async Task LoadRolesAsync(CancellationToken cancellationToken)
    {
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

            if (string.IsNullOrWhiteSpace(Role) || !AssignableRoles.Contains(Role, StringComparer.OrdinalIgnoreCase))
                Role = AssignableRoles.FirstOrDefault() ?? "User";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load roles for add user");
            ModelState.AddModelError(string.Empty, UserManagerModel.GetErrorMessage(ex, "Could not load available roles."));
            AssignableRoles = ["User"];
        }
    }
}
