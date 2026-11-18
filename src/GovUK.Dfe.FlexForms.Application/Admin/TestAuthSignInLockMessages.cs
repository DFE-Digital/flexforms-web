namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>User-facing copy for the sign-in lock section of User Manager Edit.</summary>
public static class TestAuthSignInLockMessages
{
    public const string Locked = "User locked out of sign-in. They cannot sign in until you unlock them.";

    public const string Unlocked = "User unlocked. They can sign in again.";

    public const string Unchanged = "No changes made to sign-in lock.";

    public const string TemporaryLockoutCleared = "Temporary lock removed. The user can sign in with their authenticator app again.";

    public const string LoadFailed = "Could not load the user's sign-in lock status.";

    public const string UpdateFailed = "Could not change the user's sign-in lock.";

    public static string TemporarilyLockedFor(int? retryAfterSeconds)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling((retryAfterSeconds ?? 60) / 60d));
        return minutes == 1 ? "about 1 minute" : $"about {minutes} minutes";
    }
}
