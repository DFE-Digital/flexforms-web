using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using GovUK.Dfe.FlexForms.Web.Services;
using GovUK.Dfe.FlexForms.Web.Security;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.CoreLibs.Security.Configurations;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;

namespace GovUK.Dfe.FlexForms.Web.Pages;

[AllowAnonymous]
public class TestLoginModel : PageModel
{
    public const string InvalidPasswordMessage = "The one-time password is incorrect or has expired";
    public const string PasswordFormatMessage = "Enter the 6 digit one-time password from your email";
    public const string SendFailedMessage = "We could not send your one-time password. Please try again.";
    public const string VerifyFailedMessage = "We could not check your one-time password. Please try again.";
    public const string NotEnabledMessage = "Test authentication is not enabled for this service.";

    private static readonly Regex PasswordPattern = new(@"^\d{6}$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private readonly IOptions<TestAuthenticationOptions> _testAuthOptions;
    private readonly IOptions<EntraSsoOptions> _entraSsoOptions;
    private readonly ITestAuthenticationService _testAuthenticationService;
    private readonly ITokensClient _tokensClient;
    private readonly ILogger<TestLoginModel> _logger;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>
    /// <c>true</c> once a one-time password has been emailed, so the page shows the password field.
    /// </summary>
    public bool PasswordSent { get; private set; }

    public TestLoginModel(
        IOptions<TestAuthenticationOptions> testAuthOptions,
        IOptions<EntraSsoOptions> entraSsoOptions,
        ITestAuthenticationService testAuthenticationService,
        ITokensClient tokensClient,
        ILogger<TestLoginModel> logger)
    {
        _testAuthOptions = testAuthOptions;
        _entraSsoOptions = entraSsoOptions;
        _testAuthenticationService = testAuthenticationService;
        _tokensClient = tokensClient;
        _logger = logger;
    }

    public IActionResult OnGet()
    {
        if (!IsTestAuthActive())
        {
            return NotFound();
        }

        return Page();
    }

    /// <summary>
    /// Step 1 (and "send a new password"): emails a one-time password to the entered address.
    /// </summary>
    public async Task<IActionResult> OnPostAsync()
    {
        if (!IsTestAuthActive())
        {
            return NotFound();
        }

        ModelState.Remove($"{nameof(Input)}.{nameof(InputModel.Password)}");
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await _tokensClient.SendTestAuthPasswordAsync(new SendTestAuthPasswordRequest(Input.Email));
        }
        catch (ExternalApplicationsException ex)
        {
            _logger.LogError(ex, "Failed to send test authentication password to {Email}", Input.Email);
            ErrorMessage = ex.StatusCode == StatusCodes.Status403Forbidden ? NotEnabledMessage : SendFailedMessage;
            return Page();
        }

        Input.Password = null;
        PasswordSent = true;
        return Page();
    }

    /// <summary>
    /// Step 2: verifies the one-time password and signs the user in.
    /// </summary>
    public async Task<IActionResult> OnPostVerifyAsync()
    {
        if (!IsTestAuthActive())
        {
            return NotFound();
        }

        PasswordSent = true;

        var password = Input.Password?.Trim();
        if (string.IsNullOrEmpty(password) || !PasswordPattern.IsMatch(password))
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(InputModel.Password)}", PasswordFormatMessage);
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        VerifyTestAuthPasswordResponse verification;
        try
        {
            verification = await _tokensClient.VerifyTestAuthPasswordAsync(
                new VerifyTestAuthPasswordRequest(Input.Email, password!));
        }
        catch (ExternalApplicationsException ex)
        {
            _logger.LogError(ex, "Failed to verify test authentication password for {Email}", Input.Email);
            ErrorMessage = ex.StatusCode == StatusCodes.Status403Forbidden ? NotEnabledMessage : VerifyFailedMessage;
            return Page();
        }

        if (!verification.IsValid)
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(InputModel.Password)}", InvalidPasswordMessage);
            return Page();
        }

        return await SignInAsync();
    }

    private async Task<IActionResult> SignInAsync()
    {
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

        [Display(Name = "One-time password")]
        public string? Password { get; set; }
    }
}
