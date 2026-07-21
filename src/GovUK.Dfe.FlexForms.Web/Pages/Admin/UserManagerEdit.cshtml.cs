using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Edits which forms a tenant user can access.
/// </summary>
[Authorize(Roles = "Admin")]
public sealed class UserManagerEditModel(
    IUsersClient usersClient,
    ITemplatesClient templatesClient,
    ILogger<UserManagerEditModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid UserId { get; set; }

    public string UserName { get; private set; } = string.Empty;

    public string UserEmail { get; private set; } = string.Empty;

    public string UserRole { get; private set; } = string.Empty;

    public IReadOnlyList<TemplateDto> AvailableTemplates { get; private set; } = [];

    [BindProperty]
    public List<Guid> SelectedTemplateIds { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(cancellationToken);
        return loaded ? Page() : RedirectToPage("/Admin/UserManager");
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        try
        {
            await usersClient.UpdateUserTemplateAccessAsync(
                UserId,
                new UpdateUserTemplateAccessRequest { TemplateIds = SelectedTemplateIds ?? [] },
                cancellationToken);

            TempData["UserManagerSuccess"] = "Form access updated.";
            return RedirectToPage("/Admin/UserManager");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update template access for user {UserId}", UserId);
            ModelState.AddModelError(string.Empty, UserManagerModel.GetErrorMessage(ex, "Could not update form access."));
            await LoadAsync(cancellationToken);
            return Page();
        }
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var users = await usersClient.GetTenantUsersAsync(cancellationToken);
            var templates = await templatesClient.GetAccessibleTemplatesAsync(cancellationToken);

            var user = users?.FirstOrDefault(u => u.UserId == UserId);
            if (user is null)
            {
                TempData["UserManagerError"] = "User not found in this tenant.";
                return false;
            }

            UserName = user.Name;
            UserEmail = user.Email;
            UserRole = user.Role;
            AvailableTemplates = templates?.OrderBy(t => t.Name).ToList() ?? [];

            if (SelectedTemplateIds.Count == 0)
            {
                SelectedTemplateIds = user.Templates.Select(t => t.TemplateId).ToList();
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load user {UserId} for edit", UserId);
            TempData["UserManagerError"] = UserManagerModel.GetErrorMessage(ex, "Could not load user details.");
            return false;
        }
    }
}
