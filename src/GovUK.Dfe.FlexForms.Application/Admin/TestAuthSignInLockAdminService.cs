using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Locks a user out of Test Authentication sign-in until an administrator unlocks them, unlocks them, or ends a
/// temporary lockout from wrong authenticator app codes. Kept apart from role and form access changes so one cannot
/// leave the other half-saved. The API enforces who may lock whom and audits every change.
/// </summary>
public interface ITestAuthSignInLockAdmin
{
    /// <summary>Loads the lock status into <paramref name="state"/>. Returns an error message when it could not.</summary>
    Task<string?> LoadAsync(TestAuthSignInLockWorkState state, CancellationToken cancellationToken = default);

    /// <summary>Applies <see cref="TestAuthSignInLockWorkState.LockSignIn"/>.</summary>
    Task<AdminPageOutcome> SetLockAsync(TestAuthSignInLockWorkState state, CancellationToken cancellationToken = default);

    Task<AdminPageOutcome> ClearTemporaryLockoutAsync(TestAuthSignInLockWorkState state, CancellationToken cancellationToken = default);
}

public sealed class TestAuthSignInLockAdminService(
    IUsersClient usersClient,
    ILogger<TestAuthSignInLockAdminService> logger) : ITestAuthSignInLockAdmin
{
    public async Task<string?> LoadAsync(TestAuthSignInLockWorkState state, CancellationToken cancellationToken = default)
    {
        try
        {
            Apply(state, await usersClient.GetTestAuthSignInLockAsync(state.UserId, cancellationToken));
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load the sign-in lock for user {UserId}", state.UserId);
            return AdminApiErrorMapper.Format(ex, TestAuthSignInLockMessages.LoadFailed);
        }
    }

    public async Task<AdminPageOutcome> SetLockAsync(
        TestAuthSignInLockWorkState state,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Compare with what is stored now, not with what the page showed, so a stale page cannot report a change.
            var before = await usersClient.GetTestAuthSignInLockAsync(state.UserId, cancellationToken);
            if (before.IsManuallyLocked == state.LockSignIn)
            {
                Apply(state, before);
                return AdminPageOutcome.Stay(successMessage: TestAuthSignInLockMessages.Unchanged);
            }

            Apply(state, await usersClient.SetTestAuthSignInLockAsync(
                state.UserId,
                new SetTestAuthSignInLockRequest(state.LockSignIn),
                cancellationToken));

            return AdminPageOutcome.Stay(successMessage: state.IsManuallyLocked
                ? TestAuthSignInLockMessages.Locked
                : TestAuthSignInLockMessages.Unlocked);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to change the sign-in lock for user {UserId}", state.UserId);
            return AdminPageOutcome.Stay(errorMessage: AdminApiErrorMapper.Format(ex, TestAuthSignInLockMessages.UpdateFailed));
        }
    }

    public async Task<AdminPageOutcome> ClearTemporaryLockoutAsync(
        TestAuthSignInLockWorkState state,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Apply(state, await usersClient.ClearTestAuthTemporaryLockoutAsync(state.UserId, cancellationToken));
            return AdminPageOutcome.Stay(successMessage: TestAuthSignInLockMessages.TemporaryLockoutCleared);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clear the temporary sign-in lockout for user {UserId}", state.UserId);
            return AdminPageOutcome.Stay(errorMessage: AdminApiErrorMapper.Format(ex, TestAuthSignInLockMessages.UpdateFailed));
        }
    }

    private static void Apply(TestAuthSignInLockWorkState state, TestAuthSignInLockStatusResponse status)
    {
        state.IsLoaded = true;
        state.IsManuallyLocked = status.IsManuallyLocked;
        state.LockSignIn = status.IsManuallyLocked;
        state.LockedAtUtc = status.LockedAtUtc;
        state.LockedByEmail = status.LockedByEmail;
        state.IsTemporarilyLocked = status.IsTemporarilyLocked;
        state.RetryAfterSeconds = status.RetryAfterSeconds;
    }
}
