using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using GovUK.Dfe.FlexForms.Web.Services;
using GovUK.Dfe.FlexForms.Web.Security;
using System.Diagnostics.CodeAnalysis;
using GovUK.Dfe.CoreLibs.Security.Configurations;

namespace GovUK.Dfe.FlexForms.Web.Pages;

[ExcludeFromCodeCoverage]
[AllowAnonymous]
public class TestLogoutModel : PageModel
{
    private readonly IOptions<TestAuthenticationOptions> _testAuthOptions;
    private readonly IOptions<EntraSsoOptions> _entraSsoOptions;
    private readonly ITestAuthenticationService _testAuthenticationService;

    public TestLogoutModel(
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

        await _testAuthenticationService.SignOutAsync(HttpContext);
        return Redirect("/");
    }

    private bool IsTestAuthActive()
        => TenantAuthSchemeSelector.IsTestAuthenticationActive(
            HttpContext,
            _testAuthOptions,
            _entraSsoOptions);
}
