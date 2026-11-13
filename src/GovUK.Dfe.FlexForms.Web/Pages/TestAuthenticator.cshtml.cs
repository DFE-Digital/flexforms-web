using System.Security.Claims;
using System.Text.RegularExpressions;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.CoreLibs.Security.Configurations;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.Pages;

/// <summary>
/// Lets a user signed in with Test Authentication enrol, replace or remove the authenticator app they can use
/// instead of an emailed one-time password. The API keeps the pending secret; the key and QR code URI are posted
/// back only so the page can show them again after a wrong code.
/// </summary>
[Authorize]
public class TestAuthenticatorModel : PageModel
{
    public const string CodeFormatMessage = "Enter the 6 digit code from your authenticator app";
    public const string InvalidCodeMessage =
        "The code is incorrect, or setting up has expired. Check the code in your app, or start again.";
    public const string SetupExpiredMessage = "Setting up has expired. Start again.";
    public const string NotEnabledMessage = "Test authentication is not enabled for this service.";
    public const string TooManyRequestsMessage = "You have tried too many times. Wait a minute and try again.";
    public const string FailedMessage = "Something went wrong. Please try again.";
    public const string EnrolledMessage = "Your authenticator app is set up. You can use it to sign in instead of an emailed one-time password.";
    public const string RemovedMessage = "Your authenticator app has been removed. Sign in with an emailed one-time password.";

    private static readonly Regex CodePattern = new(@"^\d{6}$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private readonly IUsersClient _usersClient;
    private readonly IOptions<TestAuthenticationOptions> _testAuthOptions;
    private readonly IOptions<EntraSsoOptions> _entraSsoOptions;
    private readonly ILogger<TestAuthenticatorModel> _logger;

    public TestAuthenticatorModel(
        IUsersClient usersClient,
        IOptions<TestAuthenticationOptions> testAuthOptions,
        IOptions<EntraSsoOptions> entraSsoOptions,
        ILogger<TestAuthenticatorModel> logger)
    {
        _usersClient = usersClient;
        _testAuthOptions = testAuthOptions;
        _entraSsoOptions = entraSsoOptions;
        _logger = logger;
    }

    [BindProperty]
    public string? Code { get; set; }

    [BindProperty]
    public string? SharedKey { get; set; }

    [BindProperty]
    public string? AuthenticatorUri { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    public string? ErrorMessage { get; private set; }

    public TestAuthAuthenticatorStatusResponse? Status { get; private set; }

    public string? QrCodeDataUri { get; private set; }

    public bool SettingUp => !string.IsNullOrEmpty(SharedKey) && AuthenticatorQrCode.IsAuthenticatorUri(AuthenticatorUri);

    public string? Email => User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value;

    /// <summary>The shared key in groups of four, which is easier to type into an app.</summary>
    public string FormattedSharedKey => AuthenticatorQrCode.FormatKey(SharedKey);

    public async Task<IActionResult> OnGetAsync()
    {
        if (!IsTestAuthActive())
            return NotFound();

        await LoadStatusAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostBeginAsync()
    {
        if (!IsTestAuthActive())
            return NotFound();

        ModelState.Clear();
        Code = null;

        try
        {
            var setup = await _usersClient.BeginMyTestAuthAuthenticatorSetupAsync();
            SharedKey = setup.SharedKey;
            AuthenticatorUri = setup.AuthenticatorUri;
            QrCodeDataUri = AuthenticatorQrCode.ToPngDataUri(setup.AuthenticatorUri);
        }
        catch (ExternalApplicationsException ex)
        {
            _logger.LogError(ex, "Failed to start test authentication authenticator setup");
            ErrorMessage = MessageFor(ex);
            SharedKey = null;
            AuthenticatorUri = null;
        }

        await LoadStatusAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmAsync()
    {
        if (!IsTestAuthActive())
            return NotFound();

        if (!SettingUp)
        {
            ErrorMessage = SetupExpiredMessage;
            await LoadStatusAsync();
            return Page();
        }

        QrCodeDataUri = AuthenticatorQrCode.ToPngDataUri(AuthenticatorUri!);

        var code = Code?.Trim() ?? string.Empty;
        if (!CodePattern.IsMatch(code))
        {
            ModelState.AddModelError(nameof(Code), CodeFormatMessage);
            await LoadStatusAsync();
            return Page();
        }

        TestAuthAuthenticatorStatusResponse result;
        try
        {
            result = await _usersClient.ConfirmMyTestAuthAuthenticatorSetupAsync(
                new ConfirmTestAuthAuthenticatorSetupRequest(code));
        }
        catch (ExternalApplicationsException ex)
        {
            _logger.LogError(ex, "Failed to confirm test authentication authenticator setup");
            ErrorMessage = MessageFor(ex);
            await LoadStatusAsync();
            return Page();
        }

        if (!result.IsEnrolled)
        {
            Code = null;
            ModelState.AddModelError(nameof(Code), InvalidCodeMessage);
            Status = result;
            return Page();
        }

        SuccessMessage = EnrolledMessage;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveAsync()
    {
        if (!IsTestAuthActive())
            return NotFound();

        try
        {
            await _usersClient.RemoveMyTestAuthAuthenticatorAsync();
        }
        catch (ExternalApplicationsException ex)
        {
            _logger.LogError(ex, "Failed to remove test authentication authenticator");
            ErrorMessage = MessageFor(ex);
            await LoadStatusAsync();
            return Page();
        }

        SuccessMessage = RemovedMessage;
        return RedirectToPage();
    }

    private async Task LoadStatusAsync()
    {
        try
        {
            Status = await _usersClient.GetMyTestAuthAuthenticatorStatusAsync();
        }
        catch (ExternalApplicationsException ex)
        {
            _logger.LogError(ex, "Failed to load test authentication authenticator status");
            ErrorMessage ??= MessageFor(ex);
        }
    }

    private static string MessageFor(ExternalApplicationsException ex) => ex.StatusCode switch
    {
        StatusCodes.Status403Forbidden => NotEnabledMessage,
        StatusCodes.Status429TooManyRequests => TooManyRequestsMessage,
        _ => FailedMessage
    };

    private bool IsTestAuthActive()
        => TenantAuthSchemeSelector.IsTestAuthenticationActive(
            HttpContext,
            _testAuthOptions,
            _entraSsoOptions);
}
