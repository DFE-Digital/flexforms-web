using System.Diagnostics.CodeAnalysis;
using GovUK.Dfe.CoreLibs.Security.Configurations;
using GovUK.Dfe.FlexForms.Api.Client.Security;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.Middleware;

/// <summary>
/// Server-side backstop for session and token management:
/// 1. IDLE TIMEOUT: If user inactive for configured minutes → force IDP logout
/// 2. ABSOLUTE TIMEOUT: If session exceeds configured hours → force IDP logout
/// 3. TOKEN REFRESH: If token within lead time of expiry → refresh when the IDP supports it
///
/// The UI warning overlay is driven client-side (SessionTimeoutBanner). This middleware
/// enforces limits on the next authenticated request.
/// </summary>
[ExcludeFromCodeCoverage]
public class ActivityBasedTokenRefreshMiddleware(
    RequestDelegate next,
    ILogger<ActivityBasedTokenRefreshMiddleware> logger,
    IOptions<TokenRefreshSettings> tokenRefreshSettings,
    IOptions<TestAuthenticationOptions> testAuthOptions)
{
    private readonly TestAuthenticationOptions _testAuthOptions = testAuthOptions.Value;

    private static readonly string[] SkipPaths =
    [
        "/health",
        "/healthz",
        "/liveness",
        "/readiness",
        "/favicon",
        "/css",
        "/js",
        "/lib",
        "/images",
        "/_framework",
        "/signin-oidc",
        "/signout-callback-oidc",
        "/Logout",
        "/Error",
        "/session" // stay-signed-in / timeout sign-out must not be blocked by idle checks
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        if (_testAuthOptions.Enabled)
        {
            await next(context);
            return;
        }

        if (ShouldSkipPath(context.Request.Path))
        {
            await next(context);
            return;
        }

        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        try
        {
            var shouldContinue = await ProcessSessionManagementAsync(context);
            if (!shouldContinue)
                return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in session management middleware");
        }

        await next(context);
    }

    private async Task<bool> ProcessSessionManagementAsync(HttpContext context)
    {
        var settings = tokenRefreshSettings.Value;
        var activityTracker = context.RequestServices.GetService<IUserActivityTracker>();
        var authStrategy = context.RequestServices.GetService<IAuthenticationSchemeStrategy>();

        if (activityTracker is null || authStrategy is null)
        {
            logger.LogDebug("Activity tracker or auth strategy not available, skipping session management");
            return true;
        }

        var userId = authStrategy.GetUserId(context) ?? "Unknown";

        if (activityTracker.HasSessionExpired(context, settings.AbsoluteTimeoutHours))
        {
            logger.LogInformation(
                "Forcing logout for user {UserId}: Session exceeded absolute timeout of {Hours} hours",
                userId,
                settings.AbsoluteTimeoutHours);

            ForceLogout(context, "session_expired");
            return false;
        }

        if (activityTracker.IsUserInactive(context, settings.InactivityThresholdMinutes))
        {
            logger.LogInformation(
                "Forcing logout for user {UserId}: Inactive for {Minutes} minutes",
                userId,
                settings.InactivityThresholdMinutes);

            ForceLogout(context, "idle_timeout");
            return false;
        }

        var refreshedOk = await TryRefreshTokenIfNeededAsync(context, authStrategy, userId, settings);
        if (!refreshedOk)
            return false;

        // Record activity for server-side idle enforcement.
        // Client-side warning uses its own activity listeners and does not depend on this.
        activityTracker.RecordActivity(context);

        return true;
    }

    /// <returns>False when the user was forced to sign out.</returns>
    private async Task<bool> TryRefreshTokenIfNeededAsync(
        HttpContext context,
        IAuthenticationSchemeStrategy authStrategy,
        string userId,
        TokenRefreshSettings settings)
    {
        try
        {
            var canRefresh = await authStrategy.CanRefreshTokenAsync(context);
            if (!canRefresh)
                return true;

            logger.LogInformation(
                "Refreshing token for user {UserId}: Token within {Minutes} minutes of expiry",
                userId,
                settings.RefreshLeadTimeMinutes);

            var refreshed = await authStrategy.RefreshExternalIdpTokenAsync(context);
            if (refreshed)
            {
                logger.LogInformation("Successfully refreshed token for user {UserId}", userId);
                return true;
            }

            logger.LogWarning(
                "Failed to refresh token for user {UserId}. Token may expire soon.",
                userId);

            return await HandleRefreshFailureAsync(context, authStrategy, userId, settings);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error attempting token refresh for user {UserId}", userId);
            return true;
        }
    }

    /// <returns>False when the user was forced to sign out.</returns>
    private async Task<bool> HandleRefreshFailureAsync(
        HttpContext context,
        IAuthenticationSchemeStrategy authStrategy,
        string userId,
        TokenRefreshSettings settings)
    {
        try
        {
            var tokenInfo = await authStrategy.GetExternalIdpTokenAsync(context);

            if (tokenInfo.IsPresent && tokenInfo.ExpiryTime.HasValue)
            {
                var minutesRemaining = (tokenInfo.ExpiryTime.Value - DateTime.UtcNow).TotalMinutes;

                if (minutesRemaining <= settings.ForceLogoutAtMinutesRemaining)
                {
                    logger.LogWarning(
                        "Token for user {UserId} expires in {Minutes:F1} minutes and refresh failed. Forcing re-authentication.",
                        userId,
                        minutesRemaining);

                    ForceLogout(context, "token_expiring");
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling refresh failure for user {UserId}", userId);
        }

        return true;
    }

    /// <summary>
    /// Redirect to the session timeout sign-out endpoint so cookie + IDP end_session run
    /// (DfE Sign-In OIDC and Entra SSO). Avoids cookie-only logout that SSO bounce-backs.
    /// </summary>
    private void ForceLogout(HttpContext context, string reason)
    {
        try
        {
            var url = $"/session/timeout-sign-out?reason={Uri.EscapeDataString(reason)}";
            context.Response.Redirect(url);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during forced logout redirect");
            context.Response.Redirect("/");
        }
    }

    private static bool ShouldSkipPath(PathString path)
    {
        if (!path.HasValue)
            return false;

        var pathValue = path.Value!;

        foreach (var skipPath in SkipPaths)
        {
            if (pathValue.StartsWith(skipPath, StringComparison.OrdinalIgnoreCase))
                return true;
        }

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

[ExcludeFromCodeCoverage]
public static class ActivityBasedTokenRefreshMiddlewareExtensions
{
    /// <summary>
    /// Adds session management middleware after UseAuthentication().
    /// </summary>
    public static IApplicationBuilder UseActivityBasedTokenRefresh(this IApplicationBuilder app)
        => app.UseMiddleware<ActivityBasedTokenRefreshMiddleware>();
}
