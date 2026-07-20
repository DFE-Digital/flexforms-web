namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Tracks user activity and session start times for timeout management.
/// Used to implement:
/// - Idle timeout: Force logout after period of inactivity
/// - Absolute timeout: Force logout after maximum session duration
/// </summary>
public interface IUserActivityTracker
{
    /// <summary>
    /// Records the current time as the user's last activity timestamp
    /// </summary>
    /// <param name="context">The HTTP context for the current request</param>
    void RecordActivity(HttpContext context);

    /// <summary>
    /// Gets the time since the user's last recorded activity
    /// </summary>
    /// <param name="context">The HTTP context for the current request</param>
    /// <returns>TimeSpan since last activity, or null if no activity has been recorded</returns>
    TimeSpan? GetTimeSinceLastActivity(HttpContext context);

    /// <summary>
    /// Records the session start time (called once when user first authenticates)
    /// </summary>
    /// <param name="context">The HTTP context for the current request</param>
    void RecordSessionStart(HttpContext context);

    /// <summary>
    /// Gets the total duration of the current session
    /// </summary>
    /// <param name="context">The HTTP context for the current request</param>
    /// <returns>TimeSpan since session started, or null if no session start recorded</returns>
    TimeSpan? GetSessionDuration(HttpContext context);

    /// <summary>
    /// Determines if the user is considered inactive based on the configured threshold
    /// </summary>
    /// <param name="context">The HTTP context for the current request</param>
    /// <param name="inactivityThresholdMinutes">Minutes of inactivity to consider user inactive</param>
    /// <returns>True if user has been inactive for at least the threshold period</returns>
    bool IsUserInactive(HttpContext context, int inactivityThresholdMinutes);

    /// <summary>
    /// Determines if the session has exceeded the absolute timeout
    /// </summary>
    /// <param name="context">The HTTP context for the current request</param>
    /// <param name="absoluteTimeoutHours">Maximum session duration in hours</param>
    /// <returns>True if session has exceeded the absolute timeout</returns>
    bool HasSessionExpired(HttpContext context, int absoluteTimeoutHours);
}
