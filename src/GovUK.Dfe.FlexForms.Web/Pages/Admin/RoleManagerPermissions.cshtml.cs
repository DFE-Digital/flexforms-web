using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Sets RolePermissions for a custom tenant role (ResourceType + ResourceKey + AccessType).
/// </summary>
[Authorize(Roles = AdminAccessHelper.AuthorizeRoles)]
public sealed class RoleManagerPermissionsModel(IRoleManagerPermissionsAdmin roleManagerPermissionsAdmin) : PageModel
{
    public const string AnyResourceKey = AdminPermissionGrants.AnyResourceKey;

    [BindProperty(SupportsGet = true)]
    public Guid RoleId { get; set; }

    public string RoleName { get; private set; } = string.Empty;

    public bool IsSystemRole { get; private set; }

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

    public IReadOnlyList<AccessType> AccessTypes { get; } = Enum.GetValues<AccessType>().ToArray();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var state = CaptureWorkState();
        var outcome = await roleManagerPermissionsAdmin.LoadAsync(state, cancellationToken);
        ApplyWorkState(state);
        return MapOutcome(outcome);
    }

    public async Task<IActionResult> OnPostAddAsync(CancellationToken cancellationToken)
    {
        var state = CaptureWorkState();
        var outcome = await roleManagerPermissionsAdmin.AddGrantAsync(state, cancellationToken);
        ApplyWorkState(state);
        return MapOutcome(outcome);
    }

    public async Task<IActionResult> OnPostRemoveAsync(string grantKey, CancellationToken cancellationToken)
    {
        ModelState.Remove(nameof(NewResourceKey));
        ModelState.Remove(nameof(NewResourceType));
        ModelState.Remove(nameof(NewAccessType));

        var state = CaptureWorkState();
        var outcome = await roleManagerPermissionsAdmin.RemoveGrantAsync(state, grantKey, cancellationToken);
        ApplyWorkState(state);
        return MapOutcome(outcome);
    }

    public static string EncodeGrantKey(ResourceType resourceType, string resourceKey, AccessType accessType) =>
        AdminPermissionGrants.EncodeGrantKey(resourceType, resourceKey, accessType);

    public static string FormatGrant(string key) =>
        AdminPermissionGrants.FormatGrant(key);

    /// <summary>
    /// Mirrors API <c>RolePermissionGrantRules</c>.
    /// </summary>
    public static string? ValidateGrant(ResourceType resourceType, string resourceKey, AccessType accessType) =>
        AdminPermissionGrants.ValidateGrant(resourceType, resourceKey, accessType);

    private RoleManagerPermissionsWorkState CaptureWorkState() =>
        new()
        {
            RoleId = RoleId,
            SelectedGrants = SelectedGrants,
            NewResourceType = NewResourceType,
            NewResourceKey = NewResourceKey,
            NewAccessType = NewAccessType
        };

    private void ApplyWorkState(RoleManagerPermissionsWorkState state)
    {
        RoleId = state.RoleId;
        RoleName = state.RoleName;
        IsSystemRole = state.IsSystemRole;
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
            TempData["RoleManagerError"] = outcome.ErrorMessage;

        return outcome.Kind == AdminPageOutcomeKind.RedirectToPage
            ? RedirectToPage("/Admin/RoleManager")
            : Page();
    }
}
