using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using GovUK.Dfe.FlexForms.Web.Services;
using System.Diagnostics.CodeAnalysis;
using GovUK.Dfe.CoreLibs.Security.Configurations;

namespace GovUK.Dfe.FlexForms.Web.Pages;

[ExcludeFromCodeCoverage]
[AllowAnonymous]
public class TestLogoutModel : PageModel
{
    private readonly TestAuthenticationOptions _testAuthOptions;
    private readonly ITestAuthenticationService _testAuthenticationService;

    public TestLogoutModel(
        IOptions<TestAuthenticationOptions> testAuthOptions,
        ITestAuthenticationService testAuthenticationService)
    {
        _testAuthOptions = testAuthOptions.Value;
        _testAuthenticationService = testAuthenticationService;
    }

    public IActionResult OnGet()
    {
        // Only allow access if test authentication is enabled
        if (!_testAuthOptions.Enabled)
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Only allow access if test authentication is enabled
        if (!_testAuthOptions.Enabled)
        {
            return NotFound();
        }

        await _testAuthenticationService.SignOutAsync(HttpContext);

        // Redirect to home page
        return Redirect("/");
    }
} 