using System.Security.Claims;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.CoreLibs.Security.Configurations;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Web.Pages;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages;

public class TestAuthenticatorModelTests
{
    private const string Email = "tester@education.gov.uk";
    private const string SharedKey = "JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP";
    private const string AuthenticatorUri = "otpauth://totp/Transfers%20%28Test%29:tester%40education.gov.uk?secret=JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP";
    private static readonly DateTime EnrolledOn = new(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc);

    private readonly IUsersClient _usersClient = Substitute.For<IUsersClient>();

    public TestAuthenticatorModelTests()
    {
        _usersClient.GetMyTestAuthAuthenticatorStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new TestAuthAuthenticatorStatusResponse(false, null));
        _usersClient.BeginMyTestAuthAuthenticatorSetupAsync(Arg.Any<CancellationToken>())
            .Returns(new BeginTestAuthAuthenticatorSetupResponse(SharedKey, AuthenticatorUri));
    }

    [Fact]
    public async Task OnGetAsync_loads_enrolment_status()
    {
        _usersClient.GetMyTestAuthAuthenticatorStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new TestAuthAuthenticatorStatusResponse(true, EnrolledOn));
        var model = CreateModel();

        Assert.IsType<PageResult>(await model.OnGetAsync());

        Assert.True(model.Status!.IsEnrolled);
        Assert.False(model.SettingUp);
        Assert.Equal(Email, model.Email);
    }

    [Theory]
    [InlineData("false", "Development")]
    [InlineData("true", "Production")]
    public async Task OnGetAsync_returns_not_found_when_test_auth_not_active(string enabled, string environment)
    {
        var model = CreateModel(testAuthEnabled: enabled, environment: environment);

        Assert.IsType<NotFoundResult>(await model.OnGetAsync());
        await _usersClient.DidNotReceiveWithAnyArgs().GetMyTestAuthAuthenticatorStatusAsync(default);
    }

    [Fact]
    public async Task OnPostBeginAsync_shows_qr_code_and_grouped_key()
    {
        var model = CreateModel();

        Assert.IsType<PageResult>(await model.OnPostBeginAsync());

        Assert.True(model.SettingUp);
        Assert.Equal(AuthenticatorUri, model.AuthenticatorUri);
        Assert.StartsWith("data:image/png;base64,", model.QrCodeDataUri);
        Assert.Equal("JBSW Y3DP EHPK 3PXP JBSW Y3DP EHPK 3PXP", model.FormattedSharedKey);
    }

    [Theory]
    [InlineData(429, TestAuthenticatorModel.TooManyRequestsMessage)]
    [InlineData(403, TestAuthenticatorModel.NotEnabledMessage)]
    [InlineData(500, TestAuthenticatorModel.FailedMessage)]
    public async Task OnPostBeginAsync_shows_error_when_api_fails(int statusCode, string expectedMessage)
    {
        _usersClient.BeginMyTestAuthAuthenticatorSetupAsync(Arg.Any<CancellationToken>()).ThrowsAsync(ApiException(statusCode));
        var model = CreateModel();

        Assert.IsType<PageResult>(await model.OnPostBeginAsync());

        Assert.False(model.SettingUp);
        Assert.Equal(expectedMessage, model.ErrorMessage);
    }

    [Fact]
    public async Task OnPostConfirmAsync_redirects_with_success_message_when_enrolled()
    {
        _usersClient.ConfirmMyTestAuthAuthenticatorSetupAsync(Arg.Any<ConfirmTestAuthAuthenticatorSetupRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TestAuthAuthenticatorStatusResponse(true, EnrolledOn));
        var model = CreateSettingUpModel(code: " 123456 ");

        Assert.IsType<RedirectToPageResult>(await model.OnPostConfirmAsync());

        Assert.Equal(TestAuthenticatorModel.EnrolledMessage, model.SuccessMessage);
        await _usersClient.Received(1).ConfirmMyTestAuthAuthenticatorSetupAsync(
            Arg.Is<ConfirmTestAuthAuthenticatorSetupRequest>(r => r.Code == "123456"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnPostConfirmAsync_keeps_showing_setup_when_code_wrong()
    {
        _usersClient.ConfirmMyTestAuthAuthenticatorSetupAsync(Arg.Any<ConfirmTestAuthAuthenticatorSetupRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TestAuthAuthenticatorStatusResponse(false, null));
        var model = CreateSettingUpModel(code: "123456");

        Assert.IsType<PageResult>(await model.OnPostConfirmAsync());

        Assert.True(model.SettingUp);
        Assert.NotNull(model.QrCodeDataUri);
        AssertCodeError(model, TestAuthenticatorModel.InvalidCodeMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("12345")]
    [InlineData("abcdef")]
    public async Task OnPostConfirmAsync_rejects_code_that_is_not_six_digits_without_calling_api(string? code)
    {
        var model = CreateSettingUpModel(code: code);

        Assert.IsType<PageResult>(await model.OnPostConfirmAsync());

        AssertCodeError(model, TestAuthenticatorModel.CodeFormatMessage);
        await _usersClient.DidNotReceiveWithAnyArgs().ConfirmMyTestAuthAuthenticatorSetupAsync(default!, default);
    }

    [Fact]
    public async Task OnPostConfirmAsync_does_not_trust_a_posted_uri_that_is_not_otpauth()
    {
        var model = CreateSettingUpModel(code: "123456");
        model.AuthenticatorUri = "https://example.com";

        Assert.IsType<PageResult>(await model.OnPostConfirmAsync());

        Assert.Equal(TestAuthenticatorModel.SetupExpiredMessage, model.ErrorMessage);
        Assert.Null(model.QrCodeDataUri);
        await _usersClient.DidNotReceiveWithAnyArgs().ConfirmMyTestAuthAuthenticatorSetupAsync(default!, default);
    }

    [Fact]
    public async Task OnPostRemoveAsync_redirects_with_removed_message()
    {
        var model = CreateModel();

        Assert.IsType<RedirectToPageResult>(await model.OnPostRemoveAsync());

        Assert.Equal(TestAuthenticatorModel.RemovedMessage, model.SuccessMessage);
        await _usersClient.Received(1).RemoveMyTestAuthAuthenticatorAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnPostRemoveAsync_returns_not_found_when_test_auth_not_active()
    {
        var model = CreateModel(testAuthEnabled: "false");

        Assert.IsType<NotFoundResult>(await model.OnPostRemoveAsync());
        await _usersClient.DidNotReceiveWithAnyArgs().RemoveMyTestAuthAuthenticatorAsync(default);
    }

    private TestAuthenticatorModel CreateSettingUpModel(string? code)
    {
        var model = CreateModel();
        model.SharedKey = SharedKey;
        model.AuthenticatorUri = AuthenticatorUri;
        model.Code = code;
        return model;
    }

    private TestAuthenticatorModel CreateModel(string testAuthEnabled = "true", string environment = "Development")
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

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, Email)], "Cookies"))
        };

        return new TestAuthenticatorModel(
            _usersClient,
            Options.Create(new TestAuthenticationOptions()),
            Options.Create(new EntraSsoOptions()),
            NullLogger<TestAuthenticatorModel>.Instance)
        {
            PageContext = new PageContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, Substitute.For<ITempDataProvider>())
        };
    }

    private static void AssertCodeError(TestAuthenticatorModel model, string expectedMessage)
    {
        Assert.True(model.ModelState.TryGetValue(nameof(TestAuthenticatorModel.Code), out var entry));
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
