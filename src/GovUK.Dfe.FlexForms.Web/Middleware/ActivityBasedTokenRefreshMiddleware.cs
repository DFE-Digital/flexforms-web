using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.CoreLibs.Security.Configurations;
using GovUK.Dfe.FlexForms.Api.Client.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;

namespace GovUK.Dfe.FlexForms.Web.Middleware;

/// <summary>
/// Middleware that implements simplified session and token management:
/// 
/// 1. IDLE TIMEOUT: If user inactive for 30 min → force logout
/// 2. ABSOLUTE TIMEOUT: If session exceeds 8 hours → force logout  
/// 3. TOKEN REFRESH: If token within 30 min of expiry → refresh automatically
/// 
/// This provides a clean, understandable security model aligned with UK government standards.
/// </summary>
[ExcludeFromCodeCoverage]
public class ActivityBasedTokenRefreshMiddleware(
    RequestDelegate next,
    ILogger<ActivityBasedTokenRefreshMiddleware> logger,
    IOptions<TokenRefreshSettings> tokenRefreshSettings,
    IOptions<TestAuthenticationOptions> testAuthOptions)
{
    private readonly TokenRefreshSettings _settings = tokenRefreshSettings.Value;
    private readonly TestAuthenticationOptions _testAuthOptions = testAuthOptions.Value;

    // Paths to skip (static assets, auth endpoints, etc.)
    private static readonly string[] SkipPaths =
    [
        "/health",
        "/favicon",
        "/css",
        "/js",
        "/lib",
        "/images",
        "/_framework",
        "/signin-oidc",
        "/signout-callback-oidc",
        "/Logout",
        "/Error"
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip if test authentication is enabled
        if (_testAuthOptions.Enabled)
        {
            await next(context);
            return;
        }

        // Skip for paths that shouldn't trigger session management
        if (ShouldSkipPath(context.Request.Path))
        {
            await next(context);
            return;
        }

        // Skip if user is not authenticated
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        try
        {
            var shouldContinue = await ProcessSessionManagementAsync(context);
            if (!shouldContinue)
            {
                return; // User was logged out, response already set
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in session management middleware");
            // Continue to next middleware even if session management fails
        }

        await next(context);
    }

    /// <summary>
    /// Process session management logic. Returns false if user was logged out.
    /// </summary>
    private async Task<bool> ProcessSessionManagementAsync(HttpContext context)
    {
        var activityTracker = context.RequestServices.GetService<IUserActivityTracker>();
        var authStrategy = context.RequestServices.GetService<IAuthenticationSchemeStrategy>();

        if (activityTracker == null || authStrategy == null)
        {
            logger.LogDebug("Activity tracker or auth strategy not available, skipping session management");
            return true;
        }

        var userId = authStrategy.GetUserId(context) ?? "Unknown";

        // CHECK 1: ABSOLUTE TIMEOUT (8 hours)
        // Force re-authentication regardless of activity
        if (activityTracker.HasSessionExpired(context, _settings.AbsoluteTimeoutHours))
        {
            logger.LogInformation(
                "Forcing logout for user {UserId}: Session exceeded absolute timeout of {Hours} hours",
                userId,
                _settings.AbsoluteTimeoutHours);
            
            await ForceLogoutAsync(context, "session_expired");
            return false;
        }

        // CHECK 2: IDLE TIMEOUT (30 minutes)
        // Force re-authentication if user was inactive
        if (activityTracker.IsUserInactive(context, _settings.InactivityThresholdMinutes))
        {
            logger.LogInformation(
                "Forcing logout for user {UserId}: Inactive for {Minutes} minutes",
                userId,
                _settings.InactivityThresholdMinutes);
            
            await ForceLogoutAsync(context, "idle_timeout");
            return false;
        }

        // CHECK 3: TOKEN REFRESH (when within 30 min of expiry)
        // Keep active users logged in by refreshing their token
        await TryRefreshTokenIfNeededAsync(context, authStrategy, userId);

        // RECORD ACTIVITY
        // Update last activity timestamp for idle timeout tracking
        // Skip if this is a timeout-check refresh (indicated by _tc query param)
        var isTimeoutCheck = context.Request.Query.ContainsKey("_tc");
        if (!isTimeoutCheck)
        {
            activityTracker.RecordActivity(context);
        }

        return true;
    }

    /// <summary>
    /// Attempt to refresh token if it's within the refresh lead time window
    /// </summary>
    private async Task TryRefreshTokenIfNeededAsync(
        HttpContext context, 
        IAuthenticationSchemeStrategy authStrategy, 
        string userId)
    {
        try
        {
            // Check if token needs refresh (within RefreshLeadTimeMinutes of expiry)
            var canRefresh = await authStrategy.CanRefreshTokenAsync(context);
            
            if (!canRefresh)
            {
                // Token has plenty of time left, no refresh needed
                return;
            }

            logger.LogInformation(
                "Refreshing token for user {UserId}: Token within {Minutes} minutes of expiry",
                userId,
                _settings.RefreshLeadTimeMinutes);

            var refreshed = await authStrategy.RefreshExternalIdpTokenAsync(context);

            if (refreshed)
            {
                logger.LogInformation("Successfully refreshed token for user {UserId}", userId);
            }
            else
            {
                logger.LogWarning(
                    "Failed to refresh token for user {UserId}. Token may expire soon.",
                    userId);
                
                // Check if we're critically low on time
                await HandleRefreshFailureAsync(context, authStrategy, userId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error attempting token refresh for user {UserId}", userId);
        }
    }

    /// <summary>
    /// Handle the case where token refresh failed and we're close to expiry
    /// </summary>
    private async Task HandleRefreshFailureAsync(
        HttpContext context, 
        IAuthenticationSchemeStrategy authStrategy, 
        string userId)
    {
        try
        {
            var tokenInfo = await authStrategy.GetExternalIdpTokenAsync(context);
            
            if (tokenInfo.IsPresent && tokenInfo.ExpiryTime.HasValue)
            {
                var minutesRemaining = (tokenInfo.ExpiryTime.Value - DateTime.UtcNow).TotalMinutes;
                
                if (minutesRemaining <= _settings.ForceLogoutAtMinutesRemaining)
                {
                    logger.LogWarning(
                        "Token for user {UserId} expires in {Minutes:F1} minutes and refresh failed. Forcing re-authentication.",
                        userId,
                        minutesRemaining);
                    
                    await ForceLogoutAsync(context, "token_expiring");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling refresh failure for user {UserId}", userId);
        }
    }

    /// <summary>
    /// Sign out the user and redirect to home page
    /// </summary>
    private async Task ForceLogoutAsync(HttpContext context, string reason)
    {
        try
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            context.Response.Redirect($"/?reason={reason}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during forced logout");
            context.Response.Redirect("/");
        }
    }

    private static bool ShouldSkipPath(PathString path)
    {
        if (!path.HasValue)
        {
            return false;
        }

        var pathValue = path.Value;
        
        foreach (var skipPath in SkipPaths)
        {
            if (pathValue.StartsWith(skipPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Skip static file extensions
        if (pathValue.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
            pathValue.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
            pathValue.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) ||
            pathValue.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            pathValue.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
            pathValue.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ||
            pathValue.EndsWith(".woff", StringComparison.OrdinalIgnoreCase) ||
            pathValue.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}

/// <summary>
/// Extension methods for registering the ActivityBasedTokenRefreshMiddleware
/// </summary>
[ExcludeFromCodeCoverage]
public static class ActivityBasedTokenRefreshMiddlewareExtensions
{
    /// <summary>
    /// Adds session management middleware to the pipeline.
    /// Handles idle timeout, absolute timeout, and token refresh.
    /// Should be added after UseAuthentication() and before UseAuthorization().
    /// </summary>
    public static IApplicationBuilder UseActivityBasedTokenRefresh(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ActivityBasedTokenRefreshMiddleware>();
    }
}
