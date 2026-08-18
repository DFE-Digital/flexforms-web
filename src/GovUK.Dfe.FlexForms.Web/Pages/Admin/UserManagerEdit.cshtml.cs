using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Security;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Edits a tenant user's role and form access.
/// </summary>
[Authorize(Policy = AdminAccessHelper.CanManageUsersPolicy)]
public sealed class UserManagerEditModel(
    IUserManagerEditAdmin userManagerEditAdmin,
    IInternalUserTokenStore tokenStore,
    IMemoryCache memoryCache) : PageModel
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
        var state = CaptureWorkState();
        var outcome = await userManagerEditAdmin.LoadAsync(state, cancellationToken);
        ApplyWorkState(state);
        return MapOutcome(outcome);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var state = CaptureWorkState();
        var loaded = await userManagerEditAdmin.LoadForUpdateAsync(state, cancellationToken);
        ApplyWorkState(state);
        if (loaded.Kind == AdminPageOutcomeKind.RedirectToPage)
            return MapOutcome(loaded);

        foreach (var error in loaded.Errors)
            ModelState.AddModelError(error.FieldKey, error.Message);

        if (!ModelState.IsValid)
            return Page();

        var outcome = await userManagerEditAdmin.UpdateAsync(state, cancellationToken);
        ApplyWorkState(state);

        if (outcome.Kind == AdminPageOutcomeKind.RedirectToPage && outcome.SuccessMessage != null)
            InvalidateActorSessionIfSelf(state.UserEmail);

        return MapOutcome(outcome);
    }

    private UserManagerEditWorkState CaptureWorkState() =>
        new()
        {
            UserId = UserId,
            Role = Role,
            SelectedTemplateIds = SelectedTemplateIds,
            IncludeTenantAdmin = AdminAccessHelper.IsSuperAdmin(User)
        };

    private void ApplyWorkState(UserManagerEditWorkState state)
    {
        UserId = state.UserId;
        UserName = state.UserName;
        UserEmail = state.UserEmail;
        Role = state.Role;
        SelectedTemplateIds = state.SelectedTemplateIds;
        AvailableTemplates = state.AvailableTemplates;
        AssignableRoles = state.AssignableRoles;
    }

    private IActionResult MapOutcome(AdminPageOutcome outcome)
    {
        foreach (var error in outcome.Errors)
            ModelState.AddModelError(error.FieldKey, error.Message);

        if (outcome.SuccessMessage != null)
            TempData["UserManagerSuccess"] = outcome.SuccessMessage;

        if (outcome.ErrorMessage != null)
            TempData["UserManagerError"] = outcome.ErrorMessage;

        return outcome.Kind == AdminPageOutcomeKind.RedirectToPage
            ? RedirectToPage("/Admin/UserManager")
            : Page();
    }

    private void InvalidateActorSessionIfSelf(string userEmail)
    {
        var actingEmail = User.FindFirstValue(ClaimTypes.Email);
        if (!string.IsNullOrWhiteSpace(actingEmail)
            && string.Equals(actingEmail, userEmail, StringComparison.OrdinalIgnoreCase))
        {
            tokenStore.ClearToken();
            UserPermissionsCache.Invalidate(memoryCache, User);
        }
    }
}
