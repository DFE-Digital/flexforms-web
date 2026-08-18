using System.ComponentModel.DataAnnotations;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Registers a user into the tenant with a role and optional form access.
/// </summary>
[Authorize(Policy = AdminAccessHelper.CanManageUsersPolicy)]
public sealed class UserManagerAddModel(IUserManagerAddAdmin userManagerAddAdmin) : PageModel
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
        var state = CaptureWorkState();
        await userManagerAddAdmin.LoadAsync(state, cancellationToken);
        ApplyWorkState(state);
        ApplyErrors(state);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var state = CaptureWorkState();
        await userManagerAddAdmin.LoadAsync(state, cancellationToken);
        ApplyWorkState(state);
        ApplyErrors(state);

        if (!ModelState.IsValid)
            return Page();

        return MapOutcome(await userManagerAddAdmin.AddAsync(state, cancellationToken), state);
    }

    private UserManagerAddWorkState CaptureWorkState() =>
        new()
        {
            Name = Name,
            Email = Email,
            Role = Role,
            SelectedTemplateIds = SelectedTemplateIds,
            IncludeTenantAdmin = AdminAccessHelper.IsSuperAdmin(User)
        };

    private void ApplyWorkState(UserManagerAddWorkState state)
    {
        Name = state.Name;
        Email = state.Email;
        Role = state.Role;
        SelectedTemplateIds = state.SelectedTemplateIds;
        AvailableTemplates = state.AvailableTemplates;
        AssignableRoles = state.AssignableRoles;
    }

    private void ApplyErrors(UserManagerAddWorkState state)
    {
        foreach (var error in state.Errors)
            ModelState.AddModelError(error.FieldKey, error.Message);
    }

    private IActionResult MapOutcome(AdminPageOutcome outcome, UserManagerAddWorkState state)
    {
        ApplyWorkState(state);
        foreach (var error in outcome.Errors)
            ModelState.AddModelError(error.FieldKey, error.Message);

        if (outcome.SuccessMessage != null)
            TempData["UserManagerSuccess"] = outcome.SuccessMessage;

        return outcome.Kind == AdminPageOutcomeKind.RedirectToPage
            ? RedirectToPage("/Admin/UserManager")
            : Page();
    }
}
