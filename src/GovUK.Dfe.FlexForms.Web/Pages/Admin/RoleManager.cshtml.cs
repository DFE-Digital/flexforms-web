using System.ComponentModel.DataAnnotations;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Lists and creates tenant roles.
/// </summary>
[Authorize(Roles = AdminAccessHelper.AuthorizeRoles)]
public sealed class RoleManagerModel(IRoleManagerAdmin roleManagerAdmin) : PageModel
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
        var state = CaptureWorkState();
        await roleManagerAdmin.LoadAsync(state, cancellationToken);
        ApplyWorkState(state);
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        var state = CaptureWorkState();
        await roleManagerAdmin.LoadAsync(state, cancellationToken);
        ApplyWorkState(state);

        if (!ModelState.IsValid)
            return Page();

        var outcome = await roleManagerAdmin.CreateAsync(state, cancellationToken);
        ApplyWorkState(state);
        return MapOutcome(outcome);
    }

    public async Task<IActionResult> OnPostCreateFromTemplateAsync(
        string templateKey,
        CancellationToken cancellationToken)
    {
        var outcome = await roleManagerAdmin.CreateFromTemplateAsync(
            CaptureWorkState(),
            templateKey,
            cancellationToken);
        return MapOutcome(outcome);
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var outcome = await roleManagerAdmin.DeleteAsync(CaptureWorkState(), roleId, cancellationToken);
        return MapOutcome(outcome);
    }

    public async Task<IActionResult> OnPostRenameAsync(
        Guid roleId,
        string name,
        CancellationToken cancellationToken)
    {
        var outcome = await roleManagerAdmin.RenameAsync(CaptureWorkState(), roleId, name, cancellationToken);
        return MapOutcome(outcome);
    }

    private RoleManagerWorkState CaptureWorkState() =>
        new() { NewRoleName = NewRoleName };

    private void ApplyWorkState(RoleManagerWorkState state)
    {
        Roles = state.Roles;
        NewRoleName = state.NewRoleName;
        if (state.HasError)
        {
            HasError = true;
            ErrorMessage = state.ErrorMessage;
        }
    }

    private IActionResult MapOutcome(AdminPageOutcome outcome)
    {
        foreach (var error in outcome.Errors)
            ModelState.AddModelError(error.FieldKey, error.Message);

        if (outcome.SuccessMessage != null)
            TempData["RoleManagerSuccess"] = outcome.SuccessMessage;

        if (outcome.ErrorMessage != null)
            TempData["RoleManagerError"] = outcome.ErrorMessage;

        return outcome.Kind == AdminPageOutcomeKind.RedirectToPage
            ? RedirectToPage()
            : Page();
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
}
