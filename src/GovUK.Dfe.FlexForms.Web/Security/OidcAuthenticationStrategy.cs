using GovUK.Dfe.CoreLibs.Security.TokenRefresh.Interfaces;
using GovUK.Dfe.FlexForms.Api.Client.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Authentication strategy for OIDC-based authentication
/// Handles DfE Sign-In and other OIDC providers
/// </summary>
public class OidcAuthenticationStrategy(ILogger<OidcAuthenticationStrategy> logger, ITokenRefreshService tokenRefreshService) : IAuthenticationSchemeStrategy
{
    /// <summary>
    /// Matches the OIDC authentication scheme name from Program.cs configuration
    /// Note: This matches OpenIdConnectDefaults.AuthenticationScheme ("OpenIdConnect")
    /// </summary>
    public string SchemeName => OpenIdConnectDefaults.AuthenticationScheme; // "OpenIdConnect"

    /// <inheritdoc />
    public async Task<TokenInfo> GetExternalIdpTokenAsync(HttpContext context)
    {
        try
        {
            var token = await context.GetTokenAsync("id_token");
            if (string.IsNullOrEmpty(token))
            {
                logger.LogDebug("No id_token found in authentication context");
                return new TokenInfo();
            }

            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);
            
            var tokenInfo = new TokenInfo
            {
                Value = token,
                ExpiryTime = jsonToken.ValidTo
            };

            logger.LogDebug(
                "Retrieved token info. Expires at: {ExpiryTime}, Minutes remaining: {MinutesRemaining:F1}",
                jsonToken.ValidTo,
                (jsonToken.ValidTo - DateTime.UtcNow).TotalMinutes);

            return tokenInfo;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get external IDP token");
            return new TokenInfo();
        }
    }

    /// <inheritdoc />
    public async Task<bool> CanRefreshTokenAsync(HttpContext context)
    {
        try
        {
            var tokenInfo = await GetExternalIdpTokenAsync(context);
            
            if (!tokenInfo.IsPresent || !tokenInfo.ExpiryTime.HasValue)
            {
                logger.LogDebug("Cannot refresh token: Token not present or has no expiry time");
                return false;
            }

            var timeUntilExpiry = tokenInfo.ExpiryTime.Value - DateTime.UtcNow;
            var minutesRemaining = timeUntilExpiry.TotalMinutes;

            // Token already expired - cannot refresh
            if (minutesRemaining <= 0)
            {
                logger.LogWarning("Token has already expired. Minutes past expiry: {MinutesPastExpiry:F1}", Math.Abs(minutesRemaining));
                return false;
            }

            var settings = context.RequestServices.GetService<IOptions<TokenRefreshSettings>>();
            var lead = settings?.Value.RefreshLeadTimeMinutes ?? 30;

            // Allow refresh if within the lead time window (regardless of how close to expiry)
            // This fixes the previous issue where tokens with ≤5 minutes remaining couldn't be refreshed
            if (minutesRemaining <= lead)
            {
                logger.LogDebug(
                    "Token can be refreshed. Minutes remaining: {MinutesRemaining:F1}, Lead time threshold: {LeadTime} minutes",
                    minutesRemaining,
                    lead);
                return true;
            }

            logger.LogDebug(
                "Token refresh not needed yet. Minutes remaining: {MinutesRemaining:F1}, Lead time threshold: {LeadTime} minutes",
                minutesRemaining,
                lead);
            
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking if token can be refreshed");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RefreshExternalIdpTokenAsync(HttpContext context)
    {
        try
        {
            var refreshToken = await context.GetTokenAsync("refresh_token");
            if (string.IsNullOrEmpty(refreshToken))
            {
                logger.LogWarning("Cannot refresh token: No refresh_token found in authentication context");
                return false;
            }

            logger.LogDebug("Attempting to refresh token using refresh_token");
            var refreshedToken = await tokenRefreshService.RefreshTokenAsync(refreshToken, CancellationToken.None);

            if (!refreshedToken.IsSuccess)
            {
                logger.LogWarning("Token refresh failed");
                return false;
            }

            if (refreshedToken.Token?.IdToken == null || refreshedToken.Token?.RefreshToken == null)
            {
                logger.LogWarning("Token refresh returned success but tokens are null");
                return false;
            }

            await UpdateAuthenticationTokenAsync(context, refreshedToken.Token.IdToken, refreshedToken.Token.RefreshToken);
            
            logger.LogInformation("Successfully refreshed OIDC token");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception occurred while refreshing token");
            return false;
        }
    }

    public string? GetUserId(HttpContext context)
    {
        // OIDC typically uses different claim types than test authentication
        var userId = context.User?.FindFirst(ClaimTypes.Email)?.Value 
                    ?? context.User?.FindFirst("email")?.Value
                    ?? context.User?.FindFirst("sub")?.Value
                    ?? context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? context.User?.Identity?.Name;
        
        return userId;
    }

    /// <summary>
    /// Update authentication context with new token
    /// For OIDC we need to update the authentication properties and set proper expiry times
    /// </summary>
    private async Task UpdateAuthenticationTokenAsync(HttpContext context, string newToken, string refreshToken)
    {
        try
        {
            // Get current authentication result
            var authResult = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (authResult.Succeeded && authResult.Properties != null)
            {
                // Parse the new token to get its expiry time
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadJwtToken(newToken);
                var expiresAt = jsonToken.ValidTo.ToString("o", System.Globalization.CultureInfo.InvariantCulture);

                // Update tokens in authentication properties
                var tokens = new[]
                {
                    new AuthenticationToken { Name = "id_token", Value = newToken },
                    new AuthenticationToken { Name = "access_token", Value = newToken },
                    new AuthenticationToken { Name = "refresh_token", Value = refreshToken },
                    new AuthenticationToken { Name = "expires_at", Value = expiresAt }
                };
                authResult.Properties.StoreTokens(tokens);

                // Update the cookie expiry to match the new token expiry
                authResult.Properties.ExpiresUtc = jsonToken.ValidTo;

                // Re-sign in with updated properties to refresh the authentication cookie
                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, authResult.Principal, authResult.Properties);
                
                logger.LogDebug("Successfully updated OIDC authentication tokens. New token expires at: {ExpiryTime}", jsonToken.ValidTo);
            }
            else
            {
                logger.LogWarning("Failed to update OIDC authentication tokens: Authentication result was not successful or properties were null");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating OIDC authentication tokens");
            throw;
        }
    }
}
