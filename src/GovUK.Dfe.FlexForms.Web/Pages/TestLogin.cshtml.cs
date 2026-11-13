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
    public const string EmailMethod = "email";
    public const string AuthenticatorMethod = "authenticator";
    public const string SetupCodeMethod = "setup-code";

    public const string InvalidPasswordMessage = "The one-time password is incorrect or has expired";
    public const string PasswordFormatMessage = "Enter the 6 digit one-time password from your email";
    public const string SendFailedMessage = "We could not send your one-time password. Please try again.";
    public const string VerifyFailedMessage = "We could not check your one-time password. Please try again.";
    public const string NotEnabledMessage = "Test authentication is not enabled for this service.";
    public const string InvalidAuthenticatorCodeMessage =
        "The code is incorrect, or you have not set up an authenticator app for this email address. You can get a one-time password by email instead.";
    public const string AuthenticatorCodeFormatMessage = "Enter the 6 digit code from your authenticator app";
    public const string AuthenticatorVerifyFailedMessage = "We could not check your authenticator app code. Please try again.";
    public const string SetupCodeRequiredMessage = "Enter the setup code an administrator gave you";
    public const string InvalidSetupCodeMessage =
        "The setup code is incorrect, has expired or has already been used. Ask an administrator for a new one.";
    public const string SetupCodeFailedMessage = "We could not check your setup code. Please try again.";
    public const string SetupExpiredMessage = "Setting up has expired. Enter your setup code again, or ask an administrator for a new one.";
    public const string InvalidSetupConfirmCodeMessage =
        "The code is incorrect, or setting up has expired. Check the code in your app and try again.";

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

    /// <summary>
    /// <c>true</c> when the user chose to sign in with their authenticator app, so the page shows the code field.
    /// </summary>
    public bool UsingAuthenticator { get; private set; }

    /// <summary><c>true</c> when the user chose to enter an administrator-issued setup code.</summary>
    public bool UsingSetupCode { get; private set; }

    /// <summary>
    /// <c>true</c> once a setup code was accepted, so the page shows the QR code and asks for a code from the new app.
    /// </summary>
    public bool SettingUpFromCode { get; private set; }

    /// <summary>Posted back only to show the QR code again after a wrong code; the API holds the pending secret.</summary>
    [BindProperty]
    public string? SharedKey { get; set; }

    [BindProperty]
    public string? AuthenticatorUri { get; set; }

    public string? QrCodeDataUri { get; private set; }

    public string FormattedSharedKey => AuthenticatorQrCode.FormatKey(SharedKey);

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
    /// Step 1 (and "send a new password" / "switch method"): emails a one-time password to the entered address, or,
    /// when the authenticator app was chosen, shows the code field. Choosing the authenticator does not reveal whether
    /// the email has one enrolled.
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

        Input.Password = null;

        if (string.Equals(Input.Method, AuthenticatorMethod, StringComparison.Ordinal))
        {
            UsingAuthenticator = true;
            return Page();
        }

        if (string.Equals(Input.Method, SetupCodeMethod, StringComparison.Ordinal))
        {
            UsingSetupCode = true;
            return Page();
        }

        Input.Method = EmailMethod;
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
        Input.Method = EmailMethod;

        if (!TryReadCode(PasswordFormatMessage, out var password))
        {
            return Page();
        }

        VerifyTestAuthPasswordResponse verification;
        try
        {
            verification = await _tokensClient.VerifyTestAuthPasswordAsync(
                new VerifyTestAuthPasswordRequest(Input.Email, password));
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

    /// <summary>
    /// Step 2 (authenticator app): verifies the code from the user's enrolled authenticator app and signs them in.
    /// </summary>
    public async Task<IActionResult> OnPostVerifyAuthenticatorAsync()
    {
        if (!IsTestAuthActive())
        {
            return NotFound();
        }

        UsingAuthenticator = true;
        Input.Method = AuthenticatorMethod;

        if (!TryReadCode(AuthenticatorCodeFormatMessage, out var code))
        {
            return Page();
        }

        VerifyTestAuthPasswordResponse verification;
        try
        {
            verification = await _tokensClient.VerifyTestAuthAuthenticatorCodeAsync(
                new VerifyTestAuthAuthenticatorCodeRequest(Input.Email, code));
        }
        catch (ExternalApplicationsException ex)
        {
            _logger.LogError(ex, "Failed to verify test authentication authenticator code for {Email}", Input.Email);
            ErrorMessage = ex.StatusCode == StatusCodes.Status403Forbidden ? NotEnabledMessage : AuthenticatorVerifyFailedMessage;
            return Page();
        }

        if (!verification.IsValid)
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(InputModel.Password)}", InvalidAuthenticatorCodeMessage);
            return Page();
        }

        return await SignInAsync();
    }

    /// <summary>
    /// Setup code, step 1: consumes the administrator-issued code and shows the QR code for the new app.
    /// </summary>
    public async Task<IActionResult> OnPostRedeemSetupCodeAsync()
    {
        if (!IsTestAuthActive())
        {
            return NotFound();
        }

        UsingSetupCode = true;
        Input.Method = SetupCodeMethod;
        ModelState.Remove($"{nameof(Input)}.{nameof(InputModel.Password)}");

        if (string.IsNullOrWhiteSpace(Input.SetupCode))
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(InputModel.SetupCode)}", SetupCodeRequiredMessage);
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        BeginTestAuthAuthenticatorSetupResponse setup;
        try
        {
            setup = await _tokensClient.RedeemTestAuthAuthenticatorInviteAsync(
                new RedeemTestAuthAuthenticatorInviteRequest(Input.Email, Input.SetupCode!.Trim()));
        }
        catch (ExternalApplicationsException ex) when (ex.StatusCode == StatusCodes.Status400BadRequest)
        {
            Input.SetupCode = null;
            ModelState.AddModelError($"{nameof(Input)}.{nameof(InputModel.SetupCode)}", InvalidSetupCodeMessage);
            return Page();
        }
        catch (ExternalApplicationsException ex)
        {
            _logger.LogError(ex, "Failed to redeem test authentication authenticator setup code for {Email}", Input.Email);
            ErrorMessage = ex.StatusCode == StatusCodes.Status403Forbidden ? NotEnabledMessage : SetupCodeFailedMessage;
            return Page();
        }

        UsingSetupCode = false;
        SettingUpFromCode = true;
        Input.SetupCode = null;
        Input.Password = null;
        SharedKey = setup.SharedKey;
        AuthenticatorUri = setup.AuthenticatorUri;
        QrCodeDataUri = AuthenticatorQrCode.ToPngDataUri(setup.AuthenticatorUri);
        return Page();
    }

    /// <summary>
    /// Setup code, step 2: saves the new app when its code matches and signs the user in.
    /// </summary>
    public async Task<IActionResult> OnPostConfirmSetupCodeAsync()
    {
        if (!IsTestAuthActive())
        {
            return NotFound();
        }

        Input.Method = SetupCodeMethod;

        if (string.IsNullOrEmpty(SharedKey) || !AuthenticatorQrCode.IsAuthenticatorUri(AuthenticatorUri))
        {
            UsingSetupCode = true;
            ErrorMessage = SetupExpiredMessage;
            return Page();
        }

        SettingUpFromCode = true;
        QrCodeDataUri = AuthenticatorQrCode.ToPngDataUri(AuthenticatorUri!);

        if (!TryReadCode(AuthenticatorCodeFormatMessage, out var code))
        {
            return Page();
        }

        VerifyTestAuthPasswordResponse confirmation;
        try
        {
            confirmation = await _tokensClient.ConfirmTestAuthAuthenticatorInviteSetupAsync(
                new VerifyTestAuthAuthenticatorCodeRequest(Input.Email, code));
        }
        catch (ExternalApplicationsException ex)
        {
            _logger.LogError(ex, "Failed to confirm test authentication authenticator setup for {Email}", Input.Email);
            ErrorMessage = ex.StatusCode == StatusCodes.Status403Forbidden ? NotEnabledMessage : AuthenticatorVerifyFailedMessage;
            return Page();
        }

        if (!confirmation.IsValid)
        {
            Input.Password = null;
            ModelState.AddModelError($"{nameof(Input)}.{nameof(InputModel.Password)}", InvalidSetupConfirmCodeMessage);
            return Page();
        }

        return await SignInAsync();
    }

    private bool TryReadCode(string formatMessage, out string code)
    {
        code = Input.Password?.Trim() ?? string.Empty;
        if (!PasswordPattern.IsMatch(code))
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(InputModel.Password)}", formatMessage);
        }

        return ModelState.IsValid;
    }

    private async Task<IActionResult> SignInAsync()
    {
        var result = await _testAuthenticationService.AuthenticateAsync(Input.Email, HttpContext);

        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            return Page();
        }

        var redirectUrl = LocalRedirects.OrFallback(
            ReturnUrl ?? result.RedirectUrl,
            "/applications/dashboard");
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

        /// <summary>
        /// How the user wants to sign in: <see cref="EmailMethod"/> (default) or <see cref="AuthenticatorMethod"/>.
        /// </summary>
        public string? Method { get; set; } = EmailMethod;

        /// <summary>The emailed one-time password, or the authenticator app code.</summary>
        [Display(Name = "One-time password")]
        public string? Password { get; set; }

        /// <summary>A one-time authenticator app setup code from an administrator.</summary>
        [Display(Name = "Setup code")]
        public string? SetupCode { get; set; }
    }
}
