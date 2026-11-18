namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Sign-in lock status for one user on User Manager Edit. Only used where Test Authentication is active.
/// </summary>
public sealed class TestAuthSignInLockWorkState
{
    public Guid UserId { get; set; }

    /// <summary>The value the administrator chose (on post) or the current lock (after load).</summary>
    public bool LockSignIn { get; set; }

    public bool IsLoaded { get; set; }

    public bool IsManuallyLocked { get; set; }

    public DateTime? LockedAtUtc { get; set; }

    public string? LockedByEmail { get; set; }

    public bool IsTemporarilyLocked { get; set; }

    public int? RetryAfterSeconds { get; set; }
}
