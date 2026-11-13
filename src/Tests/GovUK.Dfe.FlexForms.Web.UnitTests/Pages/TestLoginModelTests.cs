using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.CoreLibs.Security.Configurations;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Web.Pages;
using GovUK.Dfe.FlexForms.Web.Services;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages;

public class TestLoginModelTests
{
    private const string Email = "tester@education.gov.uk";
    private const string PasswordKey = "Input.Password";

    private readonly ITestAuthenticationService _testAuthenticationService = Substitute.For<ITestAuthenticationService>();
    private readonly ITokensClient _tokensClient = Substitute.For<ITokensClient>();

    public TestLoginModelTests()
    {
        _testAuthenticationService.AuthenticateAsync(Arg.Any<string>(), Arg.Any<HttpContext>())
            .Returns(TestAuthenticationResult.Success("/applications/dashboard"));
        _tokensClient.VerifyTestAuthPasswordAsync(Arg.Any<VerifyTestAuthPasswordRequest>(), Arg.Any<CancellationToken>())
            .Returns(new VerifyTestAuthPasswordResponse(true));
    }

    [Fact]
    public void OnGet_returns_page_when_test_auth_active()
    {
        var model = CreateModel();

        Assert.IsType<PageResult>(model.OnGet());
        Assert.False(model.PasswordSent);
    }

    [Theory]
    [InlineData("false", "Development")]
    [InlineData("true", "Production")]
    public void OnGet_returns_not_found_when_test_auth_not_active(string enabled, string environment)
    {
        var model = CreateModel(testAuthEnabled: enabled, environment: environment);

        Assert.IsType<NotFoundResult>(model.OnGet());
    }

    [Fact]
    public async Task OnPostAsync_sends_password_and_shows_password_field_without_signing_in()
    {
        var model = CreateModel();

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(model.PasswordSent);
        await _tokensClient.Received(1).SendTestAuthPasswordAsync(
            Arg.Is<SendTestAuthPasswordRequest>(r => r.Email == Email), Arg.Any<CancellationToken>());
        await _testAuthenticationService.DidNotReceiveWithAnyArgs().AuthenticateAsync(default!, default!);
    }

    [Fact]
    public async Task OnPostAsync_ignores_password_validation_errors_and_clears_password()
    {
        var model = CreateModel(password: "999999");
        model.ModelState.AddModelError(PasswordKey, "stale");

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(model.PasswordSent);
        Assert.Null(model.Input.Password);
        await _tokensClient.ReceivedWithAnyArgs(1).SendTestAuthPasswordAsync(default!, default);
    }

    [Fact]
    public async Task OnPostAsync_does_not_send_when_email_invalid()
    {
        var model = CreateModel();
        model.ModelState.AddModelError("Input.Email", "Please enter a valid email address");

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(model.PasswordSent);
        await _tokensClient.DidNotReceiveWithAnyArgs().SendTestAuthPasswordAsync(default!, default);
    }

    [Fact]
    public async Task OnPostAsync_returns_not_found_when_test_auth_not_active()
    {
        var model = CreateModel(testAuthEnabled: "false");

        Assert.IsType<NotFoundResult>(await model.OnPostAsync());
        await _tokensClient.DidNotReceiveWithAnyArgs().SendTestAuthPasswordAsync(default!, default);
    }

    [Theory]
    [InlineData(500, TestLoginModel.SendFailedMessage)]
    [InlineData(403, TestLoginModel.NotEnabledMessage)]
    public async Task OnPostAsync_shows_error_when_api_fails(int statusCode, string expectedMessage)
    {
        _tokensClient.SendTestAuthPasswordAsync(Arg.Any<SendTestAuthPasswordRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(ApiException(statusCode));
        var model = CreateModel();

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(model.PasswordSent);
        Assert.Equal(expectedMessage, model.ErrorMessage);
    }

    [Fact]
    public async Task OnPostVerifyAsync_signs_in_and_redirects_when_password_valid()
    {
        var model = CreateModel(password: " 123456 ");

        var result = await model.OnPostVerifyAsync();

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/applications/dashboard", redirect.Url);
        await _tokensClient.Received(1).VerifyTestAuthPasswordAsync(
            Arg.Is<VerifyTestAuthPasswordRequest>(r => r.Email == Email && r.Password == "123456"),
            Arg.Any<CancellationToken>());
        await _testAuthenticationService.Received(1).AuthenticateAsync(Email, Arg.Any<HttpContext>());
    }

    [Fact]
    public async Task OnPostVerifyAsync_redirects_to_return_url_when_provided()
    {
        var model = CreateModel(password: "123456");
        model.ReturnUrl = "/applications/ABC-123";

        var redirect = Assert.IsType<RedirectResult>(await model.OnPostVerifyAsync());

        Assert.Equal("/applications/ABC-123", redirect.Url);
    }

    [Fact]
    public async Task OnPostVerifyAsync_shows_error_and_does_not_sign_in_when_password_wrong()
    {
        _tokensClient.VerifyTestAuthPasswordAsync(Arg.Any<VerifyTestAuthPasswordRequest>(), Arg.Any<CancellationToken>())
            .Returns(new VerifyTestAuthPasswordResponse(false));
        var model = CreateModel(password: "123456");

        var result = await model.OnPostVerifyAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(model.PasswordSent);
        AssertPasswordError(model, TestLoginModel.InvalidPasswordMessage);
        await _testAuthenticationService.DidNotReceiveWithAnyArgs().AuthenticateAsync(default!, default!);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12a456")]
    public async Task OnPostVerifyAsync_rejects_password_that_is_not_six_digits_without_calling_api(string? password)
    {
        var model = CreateModel(password: password);

        var result = await model.OnPostVerifyAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(model.PasswordSent);
        AssertPasswordError(model, TestLoginModel.PasswordFormatMessage);
        await _tokensClient.DidNotReceiveWithAnyArgs().VerifyTestAuthPasswordAsync(default!, default);
        await _testAuthenticationService.DidNotReceiveWithAnyArgs().AuthenticateAsync(default!, default!);
    }

    [Fact]
    public async Task OnPostVerifyAsync_does_not_call_api_when_email_invalid()
    {
        var model = CreateModel(password: "123456");
        model.ModelState.AddModelError("Input.Email", "Please enter a valid email address");

        Assert.IsType<PageResult>(await model.OnPostVerifyAsync());
        await _tokensClient.DidNotReceiveWithAnyArgs().VerifyTestAuthPasswordAsync(default!, default);
    }

    [Theory]
    [InlineData(500, TestLoginModel.VerifyFailedMessage)]
    [InlineData(403, TestLoginModel.NotEnabledMessage)]
    public async Task OnPostVerifyAsync_shows_error_and_does_not_sign_in_when_api_fails(int statusCode, string expectedMessage)
    {
        _tokensClient.VerifyTestAuthPasswordAsync(Arg.Any<VerifyTestAuthPasswordRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(ApiException(statusCode));
        var model = CreateModel(password: "123456");

        var result = await model.OnPostVerifyAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal(expectedMessage, model.ErrorMessage);
        await _testAuthenticationService.DidNotReceiveWithAnyArgs().AuthenticateAsync(default!, default!);
    }

    [Fact]
    public async Task OnPostVerifyAsync_shows_error_when_sign_in_fails()
    {
        _testAuthenticationService.AuthenticateAsync(Arg.Any<string>(), Arg.Any<HttpContext>())
            .Returns(TestAuthenticationResult.Failure("Sign in failed"));
        var model = CreateModel(password: "123456");

        var result = await model.OnPostVerifyAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal("Sign in failed", model.ErrorMessage);
    }

    [Fact]
    public async Task OnPostVerifyAsync_returns_not_found_when_test_auth_not_active()
    {
        var model = CreateModel(testAuthEnabled: "true", environment: "Production", password: "123456");

        Assert.IsType<NotFoundResult>(await model.OnPostVerifyAsync());
        await _tokensClient.DidNotReceiveWithAnyArgs().VerifyTestAuthPasswordAsync(default!, default);
        await _testAuthenticationService.DidNotReceiveWithAnyArgs().AuthenticateAsync(default!, default!);
    }

    [Fact]
    public async Task OnPostAsync_with_authenticator_method_shows_code_field_without_sending_email()
    {
        var model = CreateModel(method: TestLoginModel.AuthenticatorMethod);

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(model.UsingAuthenticator);
        Assert.False(model.PasswordSent);
        await _tokensClient.DidNotReceiveWithAnyArgs().SendTestAuthPasswordAsync(default!, default);
        await _tokensClient.DidNotReceiveWithAnyArgs().VerifyTestAuthAuthenticatorCodeAsync(default!, default);
    }

    [Fact]
    public async Task OnPostAsync_with_email_method_sends_password_after_choosing_authenticator()
    {
        var model = CreateModel(method: TestLoginModel.EmailMethod);

        await model.OnPostAsync();

        Assert.True(model.PasswordSent);
        Assert.False(model.UsingAuthenticator);
        await _tokensClient.ReceivedWithAnyArgs(1).SendTestAuthPasswordAsync(default!, default);
    }

    [Fact]
    public async Task OnPostAsync_with_unknown_method_falls_back_to_email()
    {
        var model = CreateModel(method: "something-else");

        await model.OnPostAsync();

        Assert.True(model.PasswordSent);
        Assert.Equal(TestLoginModel.EmailMethod, model.Input.Method);
    }

    [Fact]
    public async Task OnPostVerifyAuthenticatorAsync_signs_in_and_redirects_when_code_valid()
    {
        _tokensClient.VerifyTestAuthAuthenticatorCodeAsync(Arg.Any<VerifyTestAuthAuthenticatorCodeRequest>(), Arg.Any<CancellationToken>())
            .Returns(new VerifyTestAuthPasswordResponse(true));
        var model = CreateModel(password: " 654321 ");

        var redirect = Assert.IsType<RedirectResult>(await model.OnPostVerifyAuthenticatorAsync());

        Assert.Equal("/applications/dashboard", redirect.Url);
        await _tokensClient.Received(1).VerifyTestAuthAuthenticatorCodeAsync(
            Arg.Is<VerifyTestAuthAuthenticatorCodeRequest>(r => r.Email == Email && r.Code == "654321"),
            Arg.Any<CancellationToken>());
        await _tokensClient.DidNotReceiveWithAnyArgs().VerifyTestAuthPasswordAsync(default!, default);
        await _testAuthenticationService.Received(1).AuthenticateAsync(Email, Arg.Any<HttpContext>());
    }

    [Fact]
    public async Task OnPostVerifyAuthenticatorAsync_shows_error_and_does_not_sign_in_when_code_wrong()
    {
        _tokensClient.VerifyTestAuthAuthenticatorCodeAsync(Arg.Any<VerifyTestAuthAuthenticatorCodeRequest>(), Arg.Any<CancellationToken>())
            .Returns(new VerifyTestAuthPasswordResponse(false));
        var model = CreateModel(password: "654321");

        var result = await model.OnPostVerifyAuthenticatorAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(model.UsingAuthenticator);
        Assert.Equal(TestLoginModel.AuthenticatorMethod, model.Input.Method);
        AssertPasswordError(model, TestLoginModel.InvalidAuthenticatorCodeMessage);
        await _testAuthenticationService.DidNotReceiveWithAnyArgs().AuthenticateAsync(default!, default!);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("12345")]
    [InlineData("12a456")]
    public async Task OnPostVerifyAuthenticatorAsync_rejects_code_that_is_not_six_digits_without_calling_api(string? code)
    {
        var model = CreateModel(password: code);

        Assert.IsType<PageResult>(await model.OnPostVerifyAuthenticatorAsync());

        AssertPasswordError(model, TestLoginModel.AuthenticatorCodeFormatMessage);
        await _tokensClient.DidNotReceiveWithAnyArgs().VerifyTestAuthAuthenticatorCodeAsync(default!, default);
    }

    [Theory]
    [InlineData(500, TestLoginModel.AuthenticatorVerifyFailedMessage)]
    [InlineData(403, TestLoginModel.NotEnabledMessage)]
    public async Task OnPostVerifyAuthenticatorAsync_shows_error_when_api_fails(int statusCode, string expectedMessage)
    {
        _tokensClient.VerifyTestAuthAuthenticatorCodeAsync(Arg.Any<VerifyTestAuthAuthenticatorCodeRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(ApiException(statusCode));
        var model = CreateModel(password: "654321");

        Assert.IsType<PageResult>(await model.OnPostVerifyAuthenticatorAsync());

        Assert.Equal(expectedMessage, model.ErrorMessage);
        await _testAuthenticationService.DidNotReceiveWithAnyArgs().AuthenticateAsync(default!, default!);
    }

    [Fact]
    public async Task OnPostVerifyAuthenticatorAsync_returns_not_found_when_test_auth_not_active()
    {
        var model = CreateModel(testAuthEnabled: "false", password: "654321");

        Assert.IsType<NotFoundResult>(await model.OnPostVerifyAuthenticatorAsync());
        await _tokensClient.DidNotReceiveWithAnyArgs().VerifyTestAuthAuthenticatorCodeAsync(default!, default);
    }

    [Fact]
    public async Task OnPostAsync_with_setup_code_method_shows_setup_code_field_without_calling_api()
    {
        var model = CreateModel(method: TestLoginModel.SetupCodeMethod);

        Assert.IsType<PageResult>(await model.OnPostAsync());

        Assert.True(model.UsingSetupCode);
        Assert.False(model.PasswordSent);
        await _tokensClient.DidNotReceiveWithAnyArgs().SendTestAuthPasswordAsync(default!, default);
        await _tokensClient.DidNotReceiveWithAnyArgs().RedeemTestAuthAuthenticatorInviteAsync(default!, default);
    }

    [Fact]
    public async Task OnPostRedeemSetupCodeAsync_shows_qr_code_when_code_accepted()
    {
        _tokensClient.RedeemTestAuthAuthenticatorInviteAsync(Arg.Any<RedeemTestAuthAuthenticatorInviteRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BeginTestAuthAuthenticatorSetupResponse(SharedKey, AuthenticatorUri));
        var model = CreateModel(setupCode: " abcde-fghjk ");

        Assert.IsType<PageResult>(await model.OnPostRedeemSetupCodeAsync());

        Assert.True(model.SettingUpFromCode);
        Assert.False(model.UsingSetupCode);
        Assert.Equal(SharedKey, model.SharedKey);
        Assert.StartsWith("data:image/png;base64,", model.QrCodeDataUri);
        Assert.Null(model.Input.SetupCode);
        await _tokensClient.Received(1).RedeemTestAuthAuthenticatorInviteAsync(
            Arg.Is<RedeemTestAuthAuthenticatorInviteRequest>(r => r.Email == Email && r.InviteCode == "abcde-fghjk"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnPostRedeemSetupCodeAsync_requires_a_code()
    {
        var model = CreateModel(setupCode: "  ");

        Assert.IsType<PageResult>(await model.OnPostRedeemSetupCodeAsync());

        AssertError(model, SetupCodeKey, TestLoginModel.SetupCodeRequiredMessage);
        await _tokensClient.DidNotReceiveWithAnyArgs().RedeemTestAuthAuthenticatorInviteAsync(default!, default);
    }

    [Fact]
    public async Task OnPostRedeemSetupCodeAsync_shows_field_error_when_code_rejected()
    {
        _tokensClient.RedeemTestAuthAuthenticatorInviteAsync(Arg.Any<RedeemTestAuthAuthenticatorInviteRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(ApiException(400));
        var model = CreateModel(setupCode: "WRONG");

        Assert.IsType<PageResult>(await model.OnPostRedeemSetupCodeAsync());

        Assert.True(model.UsingSetupCode);
        Assert.False(model.SettingUpFromCode);
        AssertError(model, SetupCodeKey, TestLoginModel.InvalidSetupCodeMessage);
    }

    [Theory]
    [InlineData(403, TestLoginModel.NotEnabledMessage)]
    [InlineData(500, TestLoginModel.SetupCodeFailedMessage)]
    public async Task OnPostRedeemSetupCodeAsync_shows_error_when_api_fails(int statusCode, string expectedMessage)
    {
        _tokensClient.RedeemTestAuthAuthenticatorInviteAsync(Arg.Any<RedeemTestAuthAuthenticatorInviteRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(ApiException(statusCode));
        var model = CreateModel(setupCode: "ABCDE-FGHJK");

        Assert.IsType<PageResult>(await model.OnPostRedeemSetupCodeAsync());

        Assert.Equal(expectedMessage, model.ErrorMessage);
    }

    [Fact]
    public async Task OnPostConfirmSetupCodeAsync_signs_in_when_app_code_valid()
    {
        _tokensClient.ConfirmTestAuthAuthenticatorInviteSetupAsync(Arg.Any<VerifyTestAuthAuthenticatorCodeRequest>(), Arg.Any<CancellationToken>())
            .Returns(new VerifyTestAuthPasswordResponse(true));
        var model = CreateSettingUpModel(password: " 123456 ");

        var redirect = Assert.IsType<RedirectResult>(await model.OnPostConfirmSetupCodeAsync());

        Assert.Equal("/applications/dashboard", redirect.Url);
        await _tokensClient.Received(1).ConfirmTestAuthAuthenticatorInviteSetupAsync(
            Arg.Is<VerifyTestAuthAuthenticatorCodeRequest>(r => r.Email == Email && r.Code == "123456"),
            Arg.Any<CancellationToken>());
        await _testAuthenticationService.Received(1).AuthenticateAsync(Email, Arg.Any<HttpContext>());
    }

    [Fact]
    public async Task OnPostConfirmSetupCodeAsync_keeps_showing_qr_code_when_app_code_wrong()
    {
        _tokensClient.ConfirmTestAuthAuthenticatorInviteSetupAsync(Arg.Any<VerifyTestAuthAuthenticatorCodeRequest>(), Arg.Any<CancellationToken>())
            .Returns(new VerifyTestAuthPasswordResponse(false));
        var model = CreateSettingUpModel(password: "123456");

        Assert.IsType<PageResult>(await model.OnPostConfirmSetupCodeAsync());

        Assert.True(model.SettingUpFromCode);
        Assert.NotNull(model.QrCodeDataUri);
        AssertPasswordError(model, TestLoginModel.InvalidSetupConfirmCodeMessage);
        await _testAuthenticationService.DidNotReceiveWithAnyArgs().AuthenticateAsync(default!, default!);
    }

    [Fact]
    public async Task OnPostConfirmSetupCodeAsync_rejects_posted_uri_that_is_not_otpauth()
    {
        var model = CreateSettingUpModel(password: "123456");
        model.AuthenticatorUri = "https://example.com";

        Assert.IsType<PageResult>(await model.OnPostConfirmSetupCodeAsync());

        Assert.True(model.UsingSetupCode);
        Assert.Equal(TestLoginModel.SetupExpiredMessage, model.ErrorMessage);
        await _tokensClient.DidNotReceiveWithAnyArgs().ConfirmTestAuthAuthenticatorInviteSetupAsync(default!, default);
    }

    [Fact]
    public async Task OnPostConfirmSetupCodeAsync_returns_not_found_when_test_auth_not_active()
    {
        var model = CreateSettingUpModel(password: "123456", testAuthEnabled: "false");

        Assert.IsType<NotFoundResult>(await model.OnPostConfirmSetupCodeAsync());
        await _tokensClient.DidNotReceiveWithAnyArgs().ConfirmTestAuthAuthenticatorInviteSetupAsync(default!, default);
    }

    private const string SetupCodeKey = "Input.SetupCode";
    private const string SharedKey = "JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP";
    private const string AuthenticatorUri = "otpauth://totp/Transfers%20%28Test%29:tester%40education.gov.uk?secret=JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP";

    private TestLoginModel CreateSettingUpModel(string? password, string testAuthEnabled = "true")
    {
        var model = CreateModel(testAuthEnabled: testAuthEnabled, password: password, method: TestLoginModel.SetupCodeMethod);
        model.SharedKey = SharedKey;
        model.AuthenticatorUri = AuthenticatorUri;
        return model;
    }

    private static void AssertError(TestLoginModel model, string key, string expectedMessage)
    {
        Assert.True(model.ModelState.TryGetValue(key, out var entry));
        Assert.Contains(entry!.Errors, e => e.ErrorMessage == expectedMessage);
    }

    private TestLoginModel CreateModel(
        string testAuthEnabled = "true",
        string environment = "Development",
        string? password = null,
        string? method = null,
        string? setupCode = null)
    {
        var services = new ServiceCollection();
        services.AddScoped<ITenantRequestContext>(_ => new TenantRequestContext
        {
            TenantId = Guid.Parse("11111111-1111-4111-8111-111111111111"),
            TenantConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TestAuthentication:Enabled"] = testAuthEnabled
                })
                .Build()
        });
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment(environment));

        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

        return new TestLoginModel(
            Options.Create(new TestAuthenticationOptions()),
            Options.Create(new EntraSsoOptions()),
            _testAuthenticationService,
            _tokensClient,
            NullLogger<TestLoginModel>.Instance)
        {
            PageContext = new PageContext { HttpContext = httpContext },
            Input = new TestLoginModel.InputModel
            {
                Email = Email,
                Password = password,
                Method = method ?? TestLoginModel.EmailMethod,
                SetupCode = setupCode
            }
        };
    }

    private static void AssertPasswordError(TestLoginModel model, string expectedMessage)
    {
        Assert.True(model.ModelState.TryGetValue(PasswordKey, out var entry));
        Assert.Contains(entry!.Errors, e => e.ErrorMessage == expectedMessage);
    }

    private static ExternalApplicationsException ApiException(int statusCode)
        => new("API error", statusCode, null, new Dictionary<string, IEnumerable<string>>(), null);

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
