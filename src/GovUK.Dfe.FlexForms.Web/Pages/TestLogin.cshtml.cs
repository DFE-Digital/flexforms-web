using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using GovUK.Dfe.FlexForms.Web.Services;
using GovUK.Dfe.FlexForms.Web.Security;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using GovUK.Dfe.CoreLibs.Security.Configurations;

namespace GovUK.Dfe.FlexForms.Web.Pages;

[ExcludeFromCodeCoverage]
[AllowAnonymous]
public class TestLoginModel : PageModel
{
    private readonly IOptions<TestAuthenticationOptions> _testAuthOptions;
    private readonly IOptions<EntraSsoOptions> _entraSsoOptions;
    private readonly ITestAuthenticationService _testAuthenticationService;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    public TestLoginModel(
        IOptions<TestAuthenticationOptions> testAuthOptions,
        IOptions<EntraSsoOptions> entraSsoOptions,
        ITestAuthenticationService testAuthenticationService)
    {
        _testAuthOptions = testAuthOptions;
        _entraSsoOptions = entraSsoOptions;
        _testAuthenticationService = testAuthenticationService;
    }

    public IActionResult OnGet()
    {
        if (!IsTestAuthActive())
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!IsTestAuthActive())
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _testAuthenticationService.AuthenticateAsync(Input.Email, HttpContext);
        
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            return Page();
        }

        var redirectUrl = ReturnUrl ?? result.RedirectUrl ?? "applications/dashboard";
        return Redirect(redirectUrl);
    }

    private bool IsTestAuthActive()
        => TenantAuthSchemeSelector.IsTestAuthenticationActive(
            HttpContext,
            _testAuthOptions,
            _entraSsoOptions);

    public class InputModel
    {
        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [Display(Name = "Email address")]
        public string Email { get; set; } = string.Empty;
    }
}
