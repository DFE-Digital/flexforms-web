using GovUK.Dfe.FlexForms.Web.Middleware;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Helpers for the in-memory user permissions cache used by <see cref="PermissionsCacheMiddleware"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public static class UserPermissionsCache
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Builds the cache key for the authenticated user's permissions payload.
    /// </summary>
    public static string GetCacheKey(ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = user.FindFirstValue(ClaimTypes.Email);
        return $"{PermissionsCacheMiddleware.PermissionsCacheKeyPrefix}{userId}{email}";
    }

    /// <summary>
    /// Removes cached permissions so the next request reloads claims from the API.
    /// </summary>
    public static void Invalidate(IMemoryCache cache, ClaimsPrincipal user)
    {
        cache.Remove(GetCacheKey(user));
    }

    /// <summary>
    /// Loads the latest permissions from the API and stores them in the in-memory cache.
    /// The API maintains its own Redis cache, which is invalidated when contributors are invited.
    /// </summary>
    public static async Task<UserAuthorizationDto?> RefreshAsync(
        IMemoryCache cache,
        IUsersClient usersClient,
        ClaimsPrincipal user,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var cacheKey = GetCacheKey(user);
        Invalidate(cache, user);

        try
        {
            var permissions = await usersClient.GetMyPermissionsAsync(cancellationToken);
            cache.Set(cacheKey, permissions, CacheDuration);
            return permissions;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client navigated away / request aborted — expected, not a permissions failure.
            logger?.LogDebug("Permissions refresh canceled for {UserId}", userId);
            return null;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to refresh user permissions for {UserId}", userId);
            return null;
        }
    }

    /// <summary>
    /// Removes permission claims from the current principal so they can be rebuilt from fresh data.
    /// </summary>
    public static void RemovePermissionClaims(ClaimsPrincipal user)
    {
        if (user.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        foreach (var claim in identity.FindAll("permission").ToList())
        {
            identity.RemoveClaim(claim);
        }
    }
}
