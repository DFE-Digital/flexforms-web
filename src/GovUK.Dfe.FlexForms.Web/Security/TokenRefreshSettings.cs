namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Configurable thresholds for token refresh, session timeouts, and forced logout.
/// Implements a simplified security model:
/// - Active users: Token refreshed when within RefreshLeadTimeMinutes of expiry
/// - Inactive users: Forced to re-authenticate after InactivityThresholdMinutes
/// - All users: Forced to re-authenticate after AbsoluteTimeoutHours regardless of activity
/// </summary>
public sealed class TokenRefreshSettings
{
    /// <summary>
    /// When remaining minutes to expiry are less than or equal to this value, refresh the token.
    /// Default: 30 minutes - token is refreshed when 30 minutes or less remain until expiry
    /// </summary>
    public int RefreshLeadTimeMinutes { get; set; } = 30;

    /// <summary>
    /// When remaining minutes to expiry are less than or equal to this value, force logout.
    /// This is a safety buffer - if refresh fails and we're this close to expiry, force re-auth.
    /// Default: 5 minutes
    /// </summary>
    public int ForceLogoutAtMinutesRemaining { get; set; } = 5;

    /// <summary>
    /// Minutes of user inactivity before forcing logout on next navigation.
    /// Default: 30 minutes - standard idle timeout for government services
    /// </summary>
    public int InactivityThresholdMinutes { get; set; } = 30;

    /// <summary>
    /// Maximum session duration in hours before forcing re-authentication, regardless of activity.
    /// Default: 8 hours - typical working day, aligns with UK government service standards
    /// </summary>
    public int AbsoluteTimeoutHours { get; set; } = 8;
}
