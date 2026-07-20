using GovUK.Dfe.CoreLibs.Security.Configurations;
using GovUK.Dfe.CoreLibs.Security.Interfaces;
using GovUK.Dfe.FlexForms.Web.Authentication;
using GovUK.Dfe.FlexForms.Api.Client.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Authentication strategy for Test authentication scheme
/// Can always "refresh" by generating new tokens using consuming app's services
/// </summary>
public class TestAuthenticationStrategy(
    ILogger<TestAuthenticationStrategy> logger,
    IUserTokenService userTokenService,
    IOptions<TestAuthenticationOptions> testAuthOptions) : IAuthenticationSchemeStrategy
{
    private readonly TestAuthenticationOptions _testAuthOptions = testAuthOptions.Value;
    
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
            
            var tokenInfo = new TokenInfo
            {
                Value = token,
                ExpiryTime = jsonToken.ValidTo
            };

            return tokenInfo;
        }
        catch (Exception ex)
        {
            return new TokenInfo();
        }
    }

    public async Task<bool> CanRefreshTokenAsync(HttpContext context)
    {
        try
        {
            // Get current token to check its expiry
            var tokenInfo = await GetExternalIdpTokenAsync(context);
            
            if (!tokenInfo.IsPresent || !tokenInfo.ExpiryTime.HasValue)
            {
                return false;
            }
            
            var timeUntilExpiry = tokenInfo.ExpiryTime.Value - DateTime.UtcNow;
            var minutesRemaining = timeUntilExpiry.TotalMinutes;

            // Allow refresh when remaining time is within the configured lead window
            var settings = context.RequestServices.GetService(typeof(Microsoft.Extensions.Options.IOptions<TokenRefreshSettings>)) as Microsoft.Extensions.Options.IOptions<TokenRefreshSettings>;
            var lead = settings?.Value.RefreshLeadTimeMinutes ?? 10;
            var forceLogoutAt = settings?.Value.ForceLogoutAtMinutesRemaining ?? 5;

            if (minutesRemaining > forceLogoutAt && minutesRemaining <= lead)
            {
                return true;
            }
            
            
            return false;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    public async Task<bool> RefreshExternalIdpTokenAsync(HttpContext context)
    {
        try
        {
            if (!_testAuthOptions.Enabled)
            {
                return false;
            }

            // Get user ID
            var userId = GetUserId(context);
            if (string.IsNullOrEmpty(userId))
            {
                return false;
            }

            // Generate a new token using the proper consuming app services
            var newToken = await GenerateNewTestTokenAsync(userId, context);
            if (string.IsNullOrEmpty(newToken))
            {
                return false;
            }

            // Update the authentication context with the new token
            await UpdateAuthenticationTokenAsync(context, newToken);
            
            return true;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    /// <summary>
    /// Generate new test token using the consuming app's UserTokenService
    /// This ensures consistent token generation with the rest of the application
    /// </summary>
    private async Task<string?> GenerateNewTestTokenAsync(string userId, HttpContext context)
    {
        try
        {
            // Create claims for the user (same as TestAuthenticationService does)
            var claims = new[]
            {
                new Claim(ClaimTypes.Email, userId),
                new Claim("sub", userId),
                new Claim(ClaimTypes.Name, userId),
                new Claim("name", userId)
            };
            
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            // Use the consuming app's UserTokenService to generate the token
            // This ensures consistency with how tokens are generated during login
            var newToken = await userTokenService.GetUserTokenAsync(principal);
            
            return newToken;
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    /// <summary>
    /// Update authentication context with new token
    /// For TestAuth we keep tokens ONLY in session to avoid large cookies; do not store tokens in auth properties.
    /// </summary>
    private async Task UpdateAuthenticationTokenAsync(HttpContext context, string newToken)
    {
        try
        {
            // Update session first (primary storage for TestAuth)
            context.Session.SetString("TestAuth:Token", newToken);

            // Get current authentication result
            var authResult = await context.AuthenticateAsync();
            if (authResult.Succeeded && authResult.Properties != null)
            {
                var tokens = new[]
                {
                    new AuthenticationToken { Name = "id_token", Value = newToken },
                    new AuthenticationToken { Name = "access_token", Value = newToken }
                };
                authResult.Properties.StoreTokens(tokens);

                // Re-sign in with updated properties (cookie remains small due to server-side ticket store)
                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, authResult.Principal, authResult.Properties);
            }
            else
            {
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public string? GetUserId(HttpContext context)
    {
        var userId = context.User?.FindFirst(ClaimTypes.Email)?.Value 
                    ?? context.User?.FindFirst("sub")?.Value
                    ?? context.User?.Identity?.Name;
        
        return userId;
    }
}
