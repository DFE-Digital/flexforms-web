using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.CoreLibs.Http.Models;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GovUK.Dfe.FlexForms.Application.Tests.Admin;

public class TestAuthAuthenticatorCodeAdminServiceTests
{
    private const string Email = "tester@education.gov.uk";
    private static readonly DateTime ExpiresAt = new(2026, 11, 14, 9, 0, 0, DateTimeKind.Utc);

    private readonly IUsersClient _usersClient = Substitute.For<IUsersClient>();
    private readonly TestAuthAuthenticatorCodeAdminService _service;

    public TestAuthAuthenticatorCodeAdminServiceTests()
    {
        _service = new TestAuthAuthenticatorCodeAdminService(
            _usersClient,
            NullLogger<TestAuthAuthenticatorCodeAdminService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_returns_code_and_expiry_for_trimmed_email()
    {
        _usersClient.CreateTestAuthAuthenticatorInviteAsync(Arg.Any<CreateTestAuthAuthenticatorInviteRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CreateTestAuthAuthenticatorInviteResponse("ABCDE-FGHJK", ExpiresAt));
        var state = new TestAuthAuthenticatorCodeWorkState { Email = $" {Email} " };

        var outcome = await _service.CreateAsync(state);

        Assert.Equal(AdminPageOutcomeKind.StayOnPage, outcome.Kind);
        Assert.Equal(TestAuthAuthenticatorCodeMessages.Created(Email), outcome.SuccessMessage);
        Assert.Equal("ABCDE-FGHJK", state.Code);
        Assert.Equal(ExpiresAt, state.ExpiresAtUtc);
        await _usersClient.Received(1).CreateTestAuthAuthenticatorInviteAsync(
            Arg.Is<CreateTestAuthAuthenticatorInviteRequest>(r => r.Email == Email),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("", TestAuthAuthenticatorCodeMessages.EmailRequired)]
    [InlineData("not-an-email", TestAuthAuthenticatorCodeMessages.EmailInvalid)]
    public async Task CreateAsync_validates_email_without_calling_api(string email, string expectedMessage)
    {
        var outcome = await _service.CreateAsync(new TestAuthAuthenticatorCodeWorkState { Email = email });

        var error = Assert.Single(outcome.Errors);
        Assert.Equal(nameof(TestAuthAuthenticatorCodeWorkState.Email), error.FieldKey);
        Assert.Equal(expectedMessage, error.Message);
        await _usersClient.DidNotReceiveWithAnyArgs().CreateTestAuthAuthenticatorInviteAsync(default!, default);
    }

    [Fact]
    public async Task CreateAsync_shows_api_message_when_not_allowed()
    {
        _usersClient.CreateTestAuthAuthenticatorInviteAsync(Arg.Any<CreateTestAuthAuthenticatorInviteRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ExternalApplicationsException<ExceptionResponse>(
                "Forbidden",
                403,
                "body",
                new Dictionary<string, IEnumerable<string>>(),
                new ExceptionResponse { Message = "Only a SuperAdmin can create an authenticator setup code for another administrator." },
                null));
        var state = new TestAuthAuthenticatorCodeWorkState { Email = Email, Code = "OLD" };

        var outcome = await _service.CreateAsync(state);

        Assert.Equal("Only a SuperAdmin can create an authenticator setup code for another administrator.", outcome.ErrorMessage);
        Assert.Null(state.Code);
    }
}
