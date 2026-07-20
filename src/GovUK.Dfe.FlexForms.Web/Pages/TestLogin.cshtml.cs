using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using GovUK.Dfe.FlexForms.Web.Services;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using GovUK.Dfe.CoreLibs.Security.Configurations;

namespace GovUK.Dfe.FlexForms.Web.Pages;

[ExcludeFromCodeCoverage]
[AllowAnonymous]
public class TestLoginModel : PageModel
{
    private readonly IConfiguration _configuration;
    private readonly TestAuthenticationOptions _testAuthOptions;
    private readonly ITestAuthenticationService _testAuthenticationService;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    public TestLoginModel(
        IConfiguration configuration,
        IOptions<TestAuthenticationOptions> testAuthOptions,
        ITestAuthenticationService testAuthenticationService)
    {
        _configuration = configuration;
        _testAuthOptions = testAuthOptions.Value;
        _testAuthenticationService = testAuthenticationService;
    }

    public IActionResult OnGet()
    {
        if (!_testAuthOptions.Enabled)
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {

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

        // Redirect to return URL or use the result's redirect URL
        var redirectUrl = ReturnUrl ?? result.RedirectUrl ?? "applications/dashboard";
        return Redirect(redirectUrl);
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [Display(Name = "Email address")]
        public string Email { get; set; } = string.Empty;
    }
} 