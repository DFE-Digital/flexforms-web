using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.CoreLibs.Security.Configurations;
using GovUK.Dfe.FlexForms.Api.Client.Security;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Edits a tenant user's role and form access, and (where Test Authentication is active) their sign-in lock.
/// </summary>
[Authorize(Policy = AdminAccessHelper.CanManageUsersPolicy)]
public sealed class UserManagerEditModel(
    IUserManagerEditAdmin userManagerEditAdmin,
    ITestAuthSignInLockAdmin signInLockAdmin,
    IInternalUserTokenStore tokenStore,
    IMemoryCache memoryCache,
    IOptions<TestAuthenticationOptions> testAuthOptions,
    IOptions<EntraSsoOptions> entraSsoOptions) : PageModel
{
    public const string SignInLockSuccessKey = "UserManagerEditSignInLockSuccess";
    public const string SignInLockErrorKey = "UserManagerEditSignInLockError";

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

    /// <summary>Posted by the sign-in lock form only. Separate from the role form so neither half-saves the other.</summary>
    [BindProperty]
    public bool LockSignIn { get; set; }

    /// <summary><c>null</c> when Test Authentication is not active for this tenant, so the section is hidden.</summary>
    public TestAuthSignInLockWorkState? SignInLock { get; private set; }

    public string? SignInLockSuccessMessage { get; private set; }

    public string? SignInLockErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var state = CaptureWorkState();
        var outcome = await userManagerEditAdmin.LoadAsync(state, cancellationToken);
        ApplyWorkState(state);

        if (outcome.Kind == AdminPageOutcomeKind.StayOnPage && IsTestAuthActive())
        {
            SignInLock = new TestAuthSignInLockWorkState { UserId = UserId };
            SignInLockErrorMessage = await signInLockAdmin.LoadAsync(SignInLock, cancellationToken);
            LockSignIn = SignInLock.LockSignIn;
            SignInLockSuccessMessage = TempData[SignInLockSuccessKey] as string;
            SignInLockErrorMessage ??= TempData[SignInLockErrorKey] as string;
        }

        return MapOutcome(outcome);
    }

    public Task<IActionResult> OnPostSignInLockAsync(CancellationToken cancellationToken)
        => ChangeSignInLockAsync(
            state => signInLockAdmin.SetLockAsync(state, cancellationToken),
            new TestAuthSignInLockWorkState { UserId = UserId, LockSignIn = LockSignIn });

    public Task<IActionResult> OnPostClearTemporaryLockoutAsync(CancellationToken cancellationToken)
        => ChangeSignInLockAsync(
            state => signInLockAdmin.ClearTemporaryLockoutAsync(state, cancellationToken),
            new TestAuthSignInLockWorkState { UserId = UserId });

    private async Task<IActionResult> ChangeSignInLockAsync(
        Func<TestAuthSignInLockWorkState, Task<AdminPageOutcome>> change,
        TestAuthSignInLockWorkState state)
    {
        if (!IsTestAuthActive())
            return NotFound();

        var outcome = await change(state);
        if (outcome.SuccessMessage != null)
            TempData[SignInLockSuccessKey] = outcome.SuccessMessage;
        if (outcome.ErrorMessage != null)
            TempData[SignInLockErrorKey] = outcome.ErrorMessage;

        // Post-redirect-get, so refreshing the page does not repeat the change.
        return RedirectToPage("/Admin/UserManagerEdit", new { userId = UserId });
    }

    private bool IsTestAuthActive()
        => TenantAuthSchemeSelector.IsTestAuthenticationActive(HttpContext, testAuthOptions, entraSsoOptions);

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
            UserPermissionsCache.Invalidate(
                memoryCache,
                User,
                HttpContext.RequestServices.GetService<ITenantRequestContext>()?.TenantId);
        }
    }
}
