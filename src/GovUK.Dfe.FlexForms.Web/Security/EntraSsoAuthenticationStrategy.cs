using GovUK.Dfe.CoreLibs.Security.Configurations;
using GovUK.Dfe.CoreLibs.Security.EntraSso;
using GovUK.Dfe.FlexForms.Api.Client.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Authentication strategy for Microsoft Entra ID SSO.
/// Handles token retrieval and refresh for Entra-authenticated sessions,
/// following the same pattern as OidcAuthenticationStrategy.
/// </summary>
public class EntraSsoAuthenticationStrategy(
    ILogger<EntraSsoAuthenticationStrategy> logger,
    IOptions<EntraSsoOptions> entraSsoOptions) : IAuthenticationSchemeStrategy
{
    /// <summary>
    /// Matches the Entra SSO authentication scheme name registered via CoreLibs
    /// </summary>
    public string SchemeName => EntraSsoDefaults.AuthenticationScheme;

    /// <inheritdoc />
    public async Task<TokenInfo> GetExternalIdpTokenAsync(HttpContext context)
    {
        try
        {
            var token = await context.GetTokenAsync("id_token");
            if (string.IsNullOrEmpty(token))
            {
                logger.LogDebug("No id_token found in Entra SSO authentication context");
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
                "Retrieved Entra SSO token info. Expires at: {ExpiryTime}, Minutes remaining: {MinutesRemaining:F1}",
                jsonToken.ValidTo,
                (jsonToken.ValidTo - DateTime.UtcNow).TotalMinutes);

            return tokenInfo;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get Entra SSO token");
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
                logger.LogDebug("Cannot refresh Entra SSO token: Token not present or has no expiry time");
                return false;
            }

            var timeUntilExpiry = tokenInfo.ExpiryTime.Value - DateTime.UtcNow;
            var minutesRemaining = timeUntilExpiry.TotalMinutes;

            if (minutesRemaining <= 0)
            {
                logger.LogWarning("Entra SSO token has already expired. Minutes past expiry: {MinutesPastExpiry:F1}", Math.Abs(minutesRemaining));
                return false;
            }

            var settings = context.RequestServices.GetService<IOptions<TokenRefreshSettings>>();
            var lead = settings?.Value.RefreshLeadTimeMinutes ?? 30;

            if (minutesRemaining <= lead)
            {
                logger.LogDebug(
                    "Entra SSO token can be refreshed. Minutes remaining: {MinutesRemaining:F1}, Lead time: {LeadTime} minutes",
                    minutesRemaining,
                    lead);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking if Entra SSO token can be refreshed");
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
                logger.LogWarning("Cannot refresh Entra SSO token: No refresh_token found");
                return false;
            }

            logger.LogDebug("Entra SSO token refresh is not yet supported via standard OIDC refresh; forcing re-authentication");
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception occurred while refreshing Entra SSO token");
            return false;
        }
    }

    /// <inheritdoc />
    public string? GetUserId(HttpContext context)
    {
        var userId = context.User?.FindFirst("preferred_username")?.Value
                    ?? context.User?.FindFirst(ClaimTypes.Email)?.Value
                    ?? context.User?.FindFirst("email")?.Value
                    ?? context.User?.FindFirst("sub")?.Value
                    ?? context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? context.User?.Identity?.Name;

        return userId;
    }
}
