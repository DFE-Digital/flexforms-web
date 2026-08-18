using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Sets user-level Permissions for a tenant member (ResourceType + ResourceKey + AccessType).
/// Does not affect permissions inherited from the user's role.
/// </summary>
[Authorize(Policy = AdminAccessHelper.CanManageUsersPolicy)]
public sealed class UserManagerPermissionsModel(IUserManagerPermissionsAdmin userManagerPermissionsAdmin) : PageModel
{
    public const string AnyResourceKey = AdminPermissionGrants.AnyResourceKey;

    [BindProperty(SupportsGet = true)]
    public Guid UserId { get; set; }

    public string UserName { get; private set; } = string.Empty;

    public string UserEmail { get; private set; } = string.Empty;

    /// <summary>
    /// Selected grants encoded as "{ResourceType}|{ResourceKey}|{AccessType}".
    /// </summary>
    [BindProperty]
    public List<string> SelectedGrants { get; set; } = [];

    [BindProperty]
    public ResourceType NewResourceType { get; set; } = ResourceType.Application;

    [BindProperty]
    public string? NewResourceKey { get; set; }

    [BindProperty]
    public AccessType NewAccessType { get; set; } = AccessType.Read;

    public IReadOnlyList<ResourceType> ResourceTypes { get; } = Enum.GetValues<ResourceType>().ToArray();

    public IReadOnlyList<AccessType> AccessTypes { get; } =
        Enum.GetValues<AccessType>().Where(a => a != AccessType.Manage).ToArray();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var state = CaptureWorkState();
        var outcome = await userManagerPermissionsAdmin.LoadAsync(state, cancellationToken);
        ApplyWorkState(state);
        return MapOutcome(outcome);
    }

    public async Task<IActionResult> OnPostAddAsync(CancellationToken cancellationToken)
    {
        var state = CaptureWorkState();
        var outcome = await userManagerPermissionsAdmin.AddGrantAsync(state, cancellationToken);
        ApplyWorkState(state);
        return MapOutcome(outcome);
    }

    public async Task<IActionResult> OnPostRemoveAsync(string grantKey, CancellationToken cancellationToken)
    {
        ModelState.Remove(nameof(NewResourceKey));
        ModelState.Remove(nameof(NewResourceType));
        ModelState.Remove(nameof(NewAccessType));

        var state = CaptureWorkState();
        var outcome = await userManagerPermissionsAdmin.RemoveGrantAsync(state, grantKey, cancellationToken);
        ApplyWorkState(state);
        return MapOutcome(outcome);
    }

    private UserManagerPermissionsWorkState CaptureWorkState() =>
        new()
        {
            UserId = UserId,
            SelectedGrants = SelectedGrants,
            NewResourceType = NewResourceType,
            NewResourceKey = NewResourceKey,
            NewAccessType = NewAccessType
        };

    private void ApplyWorkState(UserManagerPermissionsWorkState state)
    {
        UserId = state.UserId;
        UserName = state.UserName;
        UserEmail = state.UserEmail;
        SelectedGrants = state.SelectedGrants;
        NewResourceType = state.NewResourceType;
        NewResourceKey = state.NewResourceKey;
        NewAccessType = state.NewAccessType;
    }

    private IActionResult MapOutcome(AdminPageOutcome outcome)
    {
        foreach (var error in outcome.Errors)
            ModelState.AddModelError(error.FieldKey, error.Message);

        if (outcome.ErrorMessage != null)
            TempData["UserManagerError"] = outcome.ErrorMessage;

        return outcome.Kind == AdminPageOutcomeKind.RedirectToPage
            ? RedirectToPage("/Admin/UserManager")
            : Page();
    }
}
