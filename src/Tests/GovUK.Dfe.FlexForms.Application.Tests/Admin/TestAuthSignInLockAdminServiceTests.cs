using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.CoreLibs.Http.Models;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GovUK.Dfe.FlexForms.Application.Tests.Admin;

public class TestAuthSignInLockAdminServiceTests
{
    private static readonly Guid UserId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly DateTime LockedAt = new(2026, 11, 17, 9, 0, 0, DateTimeKind.Utc);

    private static readonly TestAuthSignInLockStatusResponse NotLocked = new(false, null, null, false, null);
    private static readonly TestAuthSignInLockStatusResponse ManuallyLocked = new(true, LockedAt, "admin@education.gov.uk", false, null);
    private static readonly TestAuthSignInLockStatusResponse TemporarilyLocked = new(false, null, null, true, 300);

    private readonly IUsersClient _usersClient = Substitute.For<IUsersClient>();
    private readonly TestAuthSignInLockAdminService _service;

    public TestAuthSignInLockAdminServiceTests()
    {
        _service = new TestAuthSignInLockAdminService(_usersClient, NullLogger<TestAuthSignInLockAdminService>.Instance);
    }

    [Fact]
    public async Task LoadAsync_fills_status()
    {
        _usersClient.GetTestAuthSignInLockAsync(UserId, Arg.Any<CancellationToken>()).Returns(ManuallyLocked);
        var state = new TestAuthSignInLockWorkState { UserId = UserId };

        Assert.Null(await _service.LoadAsync(state));

        Assert.True(state.IsLoaded);
        Assert.True(state.IsManuallyLocked);
        Assert.True(state.LockSignIn);
        Assert.Equal(LockedAt, state.LockedAtUtc);
        Assert.Equal("admin@education.gov.uk", state.LockedByEmail);
    }

    [Fact]
    public async Task LoadAsync_returns_an_error_and_leaves_the_section_unloaded_when_the_api_fails()
    {
        _usersClient.GetTestAuthSignInLockAsync(UserId, Arg.Any<CancellationToken>()).ThrowsAsync(ApiException(500));
        var state = new TestAuthSignInLockWorkState { UserId = UserId };

        Assert.StartsWith(TestAuthSignInLockMessages.LoadFailed, await _service.LoadAsync(state));
        Assert.False(state.IsLoaded);
    }

    [Fact]
    public async Task SetLockAsync_locks_when_ticked()
    {
        _usersClient.GetTestAuthSignInLockAsync(UserId, Arg.Any<CancellationToken>()).Returns(NotLocked);
        _usersClient.SetTestAuthSignInLockAsync(UserId, Arg.Any<SetTestAuthSignInLockRequest>(), Arg.Any<CancellationToken>())
            .Returns(ManuallyLocked);
        var state = new TestAuthSignInLockWorkState { UserId = UserId, LockSignIn = true };

        var outcome = await _service.SetLockAsync(state);

        Assert.Equal(TestAuthSignInLockMessages.Locked, outcome.SuccessMessage);
        await _usersClient.Received(1).SetTestAuthSignInLockAsync(
            UserId,
            Arg.Is<SetTestAuthSignInLockRequest>(r => r.IsLocked),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetLockAsync_unlocks_when_unticked()
    {
        _usersClient.GetTestAuthSignInLockAsync(UserId, Arg.Any<CancellationToken>()).Returns(ManuallyLocked);
        _usersClient.SetTestAuthSignInLockAsync(UserId, Arg.Any<SetTestAuthSignInLockRequest>(), Arg.Any<CancellationToken>())
            .Returns(NotLocked);
        var state = new TestAuthSignInLockWorkState { UserId = UserId, LockSignIn = false };

        var outcome = await _service.SetLockAsync(state);

        Assert.Equal(TestAuthSignInLockMessages.Unlocked, outcome.SuccessMessage);
        await _usersClient.Received(1).SetTestAuthSignInLockAsync(
            UserId,
            Arg.Is<SetTestAuthSignInLockRequest>(r => !r.IsLocked),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetLockAsync_does_not_turn_a_temporary_lock_into_a_permanent_one_when_left_unticked()
    {
        _usersClient.GetTestAuthSignInLockAsync(UserId, Arg.Any<CancellationToken>()).Returns(TemporarilyLocked);
        var state = new TestAuthSignInLockWorkState { UserId = UserId, LockSignIn = false };

        var outcome = await _service.SetLockAsync(state);

        Assert.Equal(TestAuthSignInLockMessages.Unchanged, outcome.SuccessMessage);
        await _usersClient.DidNotReceiveWithAnyArgs().SetTestAuthSignInLockAsync(default, default!, default);
    }

    [Fact]
    public async Task SetLockAsync_shows_the_api_message_when_not_allowed()
    {
        _usersClient.GetTestAuthSignInLockAsync(UserId, Arg.Any<CancellationToken>()).Returns(NotLocked);
        _usersClient.SetTestAuthSignInLockAsync(UserId, Arg.Any<SetTestAuthSignInLockRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(ApiException(403, "Only a SuperAdmin can lock or unlock another administrator's sign-in."));

        var outcome = await _service.SetLockAsync(new TestAuthSignInLockWorkState { UserId = UserId, LockSignIn = true });

        Assert.Equal("Only a SuperAdmin can lock or unlock another administrator's sign-in.", outcome.ErrorMessage);
    }

    [Fact]
    public async Task ClearTemporaryLockoutAsync_clears_it()
    {
        _usersClient.ClearTestAuthTemporaryLockoutAsync(UserId, Arg.Any<CancellationToken>()).Returns(NotLocked);
        var state = new TestAuthSignInLockWorkState { UserId = UserId };

        var outcome = await _service.ClearTemporaryLockoutAsync(state);

        Assert.Equal(TestAuthSignInLockMessages.TemporaryLockoutCleared, outcome.SuccessMessage);
        Assert.False(state.IsTemporarilyLocked);
    }

    [Theory]
    [InlineData(null, "about 1 minute")]
    [InlineData(59, "about 1 minute")]
    [InlineData(61, "about 2 minutes")]
    [InlineData(900, "about 15 minutes")]
    public void TemporarilyLockedFor_rounds_up_to_whole_minutes(int? seconds, string expected)
        => Assert.Equal(expected, TestAuthSignInLockMessages.TemporarilyLockedFor(seconds));

    private static ExternalApplicationsException<ExceptionResponse> ApiException(int statusCode, string? message = null)
        => new(
            "API error",
            statusCode,
            null,
            new Dictionary<string, IEnumerable<string>>(),
            new ExceptionResponse { Message = message },
            null);
}
