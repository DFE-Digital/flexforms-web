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
    /// <summary>
    /// Short TTL only. API Redis is the source of truth and is invalidated when grants change
    /// </summary>
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Builds the cache key for the authenticated user's permissions payload.
    /// Includes tenant id when present so multi-tenant sessions do not share grants.
    /// </summary>
    public static string GetCacheKey(ClaimsPrincipal user, Guid? tenantId = null)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = user.FindFirstValue(ClaimTypes.Email);
        var tenantPart = tenantId is { } id && id != Guid.Empty
            ? id.ToString("N")
            : user.FindFirstValue("tenant_id")
              ?? user.FindFirstValue("TenantId")
              ?? "none";
        return $"{PermissionsCacheMiddleware.PermissionsCacheKeyPrefix}{tenantPart}_{userId}{email}";
    }

    /// <summary>
    /// Removes cached permissions so the next request reloads claims from the API.
    /// </summary>
    public static void Invalidate(IMemoryCache cache, ClaimsPrincipal user, Guid? tenantId = null)
    {
        cache.Remove(GetCacheKey(user, tenantId));
    }

    /// <summary>
    /// Returns cached permissions when present; otherwise loads from the API and caches briefly.
    /// Pass <paramref name="forceRefresh"/> after mutations that change the caller's grants
    /// (e.g. creating an application or template), or when grants may have changed for another user
    /// (contributor invite) — prefer force-refresh on authenticated page loads so invitees can edit
    /// immediately rather than waiting for the web TTL.
    /// </summary>
    public static async Task<UserAuthorizationDto?> RefreshAsync(
        IMemoryCache cache,
        IUsersClient usersClient,
        ClaimsPrincipal user,
        ILogger? logger = null,
        CancellationToken cancellationToken = default,
        bool forceRefresh = false,
        Guid? tenantId = null)
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

        var cacheKey = GetCacheKey(user, tenantId);
        if (!forceRefresh && cache.TryGetValue(cacheKey, out UserAuthorizationDto? cached) && cached is not null)
        {
            return cached;
        }

        if (forceRefresh)
        {
            Invalidate(cache, user, tenantId);
        }

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
