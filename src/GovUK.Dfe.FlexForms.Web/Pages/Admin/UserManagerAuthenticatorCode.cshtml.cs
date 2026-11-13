using GovUK.Dfe.CoreLibs.Security.Configurations;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Creates a one-time Test Authentication authenticator app setup code for a user who cannot receive the first
/// emailed password. Only available where Test Authentication is active.
/// </summary>
[Authorize(Policy = AdminAccessHelper.CanManageUsersPolicy)]
public sealed class UserManagerAuthenticatorCodeModel(
    ITestAuthAuthenticatorCodeAdmin codeAdmin,
    IOptions<TestAuthenticationOptions> testAuthOptions,
    IOptions<EntraSsoOptions> entraSsoOptions) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Email { get; set; } = string.Empty;

    public string? Code { get; private set; }

    public DateTime? ExpiresAtUtc { get; private set; }

    public string? SuccessMessage { get; private set; }

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet()
        => IsTestAuthActive() ? Page() : NotFound();

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!IsTestAuthActive())
            return NotFound();

        var state = new TestAuthAuthenticatorCodeWorkState { Email = Email ?? string.Empty };
        var outcome = await codeAdmin.CreateAsync(state, cancellationToken);

        Email = state.Email;
        Code = state.Code;
        ExpiresAtUtc = state.ExpiresAtUtc;
        SuccessMessage = outcome.SuccessMessage;
        ErrorMessage = outcome.ErrorMessage;
        foreach (var error in outcome.Errors)
            ModelState.AddModelError(error.FieldKey, error.Message);

        return Page();
    }

    private bool IsTestAuthActive()
        => TenantAuthSchemeSelector.IsTestAuthenticationActive(HttpContext, testAuthOptions, entraSsoOptions);
}
