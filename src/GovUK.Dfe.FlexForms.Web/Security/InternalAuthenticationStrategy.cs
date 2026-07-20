using GovUK.Dfe.FlexForms.Web.Authentication;
using GovUK.Dfe.CoreLibs.Security.Configurations;
using GovUK.Dfe.CoreLibs.Security.Interfaces;
using GovUK.Dfe.FlexForms.Api.Client.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Authentication strategy for Test authentication scheme
/// Can always "refresh" by generating new tokens using consuming app's services
/// </summary>
public class InternalAuthenticationStrategy(
    ILogger<InternalAuthenticationStrategy> logger,
    IUserTokenService userTokenService,
    IOptions<InternalServiceAuthOptions> internalAuthOptions) : IAuthenticationSchemeStrategy
{
    private readonly InternalServiceAuthOptions _internalAuthOptions = internalAuthOptions.Value;
    
    /// <summary>
    /// Matches the actual scheme name 
    /// </summary>
    public string SchemeName => InternalServiceAuthenticationHandler.SchemeName; // "InternalServiceAuth"

    public async Task<TokenInfo> GetExternalIdpTokenAsync(HttpContext context)
    {
        try
        {
            // First check session storage (primary storage for TestAuth)
            var token = context.Session.GetString("InternalAuth:Token");
            
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

    public Task<bool> CanRefreshTokenAsync(HttpContext context)
    {
        return Task.FromResult(false);
    }

    public Task<bool> RefreshExternalIdpTokenAsync(HttpContext context)
    {
        return Task.FromResult(false);
    }

    public string? GetUserId(HttpContext context)
    {
        var userId = context.User?.FindFirst(ClaimTypes.Email)?.Value 
                    ?? context.User?.FindFirst("sub")?.Value
                    ?? context.User?.Identity?.Name;
        
        return userId;
    }
}
