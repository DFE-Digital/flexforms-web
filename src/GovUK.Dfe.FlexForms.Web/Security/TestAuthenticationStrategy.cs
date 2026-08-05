using GovUK.Dfe.CoreLibs.Security.Configurations;
using GovUK.Dfe.FlexForms.Web.Authentication;
using GovUK.Dfe.FlexForms.Api.Client.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Authentication strategy for Test authentication scheme.
/// Can always "refresh" by generating new tokens from tenant TestAuthentication options.
/// </summary>
public class TestAuthenticationStrategy(
    ILogger<TestAuthenticationStrategy> logger,
    IOptions<TestAuthenticationOptions> testAuthOptions) : IAuthenticationSchemeStrategy
{
    /// <summary>
    /// Matches the actual scheme name from TestAuthenticationHandler
    /// </summary>
    public string SchemeName => TestAuthenticationHandler.SchemeName; // "TestAuthentication"

    public async Task<TokenInfo> GetExternalIdpTokenAsync(HttpContext context)
    {
        try
        {
            // First check session storage (primary storage for TestAuth)
            var token = context.Session.GetString("TestAuth:Token");
            
            // Fallback to authentication properties if not in session
            if (string.IsNullOrEmpty(token))
            {
                token = await context.GetTokenAsync("id_token");
            }
            
            if (string.IsNullOrEmpty(token))
            {
                return new TokenInfo();
            }

            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);
            
            return new TokenInfo
            {
                Value = token,
                ExpiryTime = jsonToken.ValidTo
            };
        }
        catch (Exception)
        {
            return new TokenInfo();
        }
    }

    public async Task<bool> CanRefreshTokenAsync(HttpContext context)
    {
        try
        {
            var tokenInfo = await GetExternalIdpTokenAsync(context);
            
            if (!tokenInfo.IsPresent || !tokenInfo.ExpiryTime.HasValue)
            {
                return false;
            }
            
            var timeUntilExpiry = tokenInfo.ExpiryTime.Value - DateTime.UtcNow;
            var minutesRemaining = timeUntilExpiry.TotalMinutes;

            var settings = context.RequestServices.GetService(typeof(IOptions<TokenRefreshSettings>)) as IOptions<TokenRefreshSettings>;
            var lead = settings?.Value.RefreshLeadTimeMinutes ?? 10;
            var forceLogoutAt = settings?.Value.ForceLogoutAtMinutesRemaining ?? 5;

            return minutesRemaining > forceLogoutAt && minutesRemaining <= lead;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> RefreshExternalIdpTokenAsync(HttpContext context)
    {
        try
        {
            if (!testAuthOptions.Value.Enabled
                && !TenantAuthSchemeSelector.IsTestAuthenticationActive(context, testAuthOptions))
            {
                return false;
            }

            var userId = GetUserId(context);
            if (string.IsNullOrEmpty(userId))
            {
                return false;
            }

            var newToken = GenerateNewTestToken(userId);
            if (string.IsNullOrEmpty(newToken))
            {
                return false;
            }

            await UpdateAuthenticationTokenAsync(context, newToken);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private string? GenerateNewTestToken(string userId)
    {
        try
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Email, userId),
                new Claim("sub", userId),
                new Claim(ClaimTypes.Name, userId),
                new Claim("name", userId)
            };
            
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            return TestAuthJwtFactory.CreateToken(principal, testAuthOptions.Value);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to mint refreshed test authentication token");
            return null;
        }
    }

    /// <summary>
    /// Update authentication context with new token.
    /// For TestAuth we keep tokens ONLY in session to avoid large cookies; do not store tokens in auth properties.
    /// </summary>
    private static async Task UpdateAuthenticationTokenAsync(HttpContext context, string newToken)
    {
        context.Session.SetString("TestAuth:Token", newToken);

        var authResult = await context.AuthenticateAsync();
        if (authResult.Succeeded && authResult.Properties != null)
        {
            var tokens = new[]
            {
                new AuthenticationToken { Name = "id_token", Value = newToken },
                new AuthenticationToken { Name = "access_token", Value = newToken }
            };
            authResult.Properties.StoreTokens(tokens);

            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                authResult.Principal!,
                authResult.Properties);
        }
    }

    public string? GetUserId(HttpContext context)
    {
        return context.User?.FindFirst(ClaimTypes.Email)?.Value 
                    ?? context.User?.FindFirst("sub")?.Value
                    ?? context.User?.Identity?.Name;
    }
}
