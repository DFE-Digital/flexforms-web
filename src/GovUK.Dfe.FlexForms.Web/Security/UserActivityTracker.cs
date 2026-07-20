using System.Globalization;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Session-based implementation of user activity and session tracking.
/// Stores timestamps in the user's session to track:
/// - Last activity time (for idle timeout)
/// - Session start time (for absolute timeout)
/// </summary>
public class UserActivityTracker(ILogger<UserActivityTracker> logger) : IUserActivityTracker
{
    private const string LastActivityKey = "Session:LastActivity";
    private const string SessionStartKey = "Session:StartTime";
    private const string DateFormat = "o"; // ISO 8601 format for reliable parsing

    /// <inheritdoc />
    public void RecordActivity(HttpContext context)
    {
        try
        {
            if (!IsSessionAvailable(context))
            {
                return;
            }

            var now = DateTime.UtcNow.ToString(DateFormat, CultureInfo.InvariantCulture);
            context.Session.SetString(LastActivityKey, now);
            
            // Also ensure session start is recorded (first activity = session start)
            if (string.IsNullOrEmpty(context.Session.GetString(SessionStartKey)))
            {
                context.Session.SetString(SessionStartKey, now);
                logger.LogDebug("Session started at {SessionStart}", now);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to record user activity timestamp");
        }
    }

    /// <inheritdoc />
    public TimeSpan? GetTimeSinceLastActivity(HttpContext context)
    {
        try
        {
            if (!IsSessionAvailable(context))
            {
                return null;
            }

            var lastActivityStr = context.Session.GetString(LastActivityKey);
            if (string.IsNullOrEmpty(lastActivityStr))
            {
                return null;
            }

            if (DateTime.TryParse(lastActivityStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var lastActivity))
            {
                return DateTime.UtcNow - lastActivity;
            }

            logger.LogWarning("Failed to parse last activity timestamp: {Timestamp}", lastActivityStr);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get time since last activity");
            return null;
        }
    }

    /// <inheritdoc />
    public void RecordSessionStart(HttpContext context)
    {
        try
        {
            if (!IsSessionAvailable(context))
            {
                return;
            }

            // Only record if not already set (session start is immutable)
            if (!string.IsNullOrEmpty(context.Session.GetString(SessionStartKey)))
            {
                return;
            }

            var now = DateTime.UtcNow.ToString(DateFormat, CultureInfo.InvariantCulture);
            context.Session.SetString(SessionStartKey, now);
            
            logger.LogDebug("Session started at {SessionStart}", now);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to record session start timestamp");
        }
    }

    /// <inheritdoc />
    public TimeSpan? GetSessionDuration(HttpContext context)
    {
        try
        {
            if (!IsSessionAvailable(context))
            {
                return null;
            }

            var sessionStartStr = context.Session.GetString(SessionStartKey);
            if (string.IsNullOrEmpty(sessionStartStr))
            {
                return null;
            }

            if (DateTime.TryParse(sessionStartStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var sessionStart))
            {
                return DateTime.UtcNow - sessionStart;
            }

            logger.LogWarning("Failed to parse session start timestamp: {Timestamp}", sessionStartStr);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get session duration");
            return null;
        }
    }

    /// <inheritdoc />
    public bool IsUserInactive(HttpContext context, int inactivityThresholdMinutes)
    {
        var timeSinceActivity = GetTimeSinceLastActivity(context);
        
        if (!timeSinceActivity.HasValue)
        {
            // No recorded activity - treat as first request, not inactive
            return false;
        }

        var isInactive = timeSinceActivity.Value.TotalMinutes >= inactivityThresholdMinutes;
        
        if (isInactive)
        {
            logger.LogInformation(
                "User idle timeout triggered. Inactive for {Minutes:F1} minutes (threshold: {Threshold} minutes)",
                timeSinceActivity.Value.TotalMinutes,
                inactivityThresholdMinutes);
        }

        return isInactive;
    }

    /// <inheritdoc />
    public bool HasSessionExpired(HttpContext context, int absoluteTimeoutHours)
    {
        var sessionDuration = GetSessionDuration(context);
        
        if (!sessionDuration.HasValue)
        {
            // No session start recorded - not expired
            return false;
        }

        var hasExpired = sessionDuration.Value.TotalHours >= absoluteTimeoutHours;
        
        if (hasExpired)
        {
            logger.LogInformation(
                "Session absolute timeout triggered. Session duration: {Hours:F1} hours (limit: {Limit} hours)",
                sessionDuration.Value.TotalHours,
                absoluteTimeoutHours);
        }

        return hasExpired;
    }

    private static bool IsSessionAvailable(HttpContext context)
    {
        try
        {
            return context.Session != null && context.Session.IsAvailable;
        }
        catch
        {
            return false;
        }
    }
}
