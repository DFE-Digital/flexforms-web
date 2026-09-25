using System.Text;
using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Api.Auth;

/// <summary>
/// Signs an internal service JWT matching InternalServiceAuthenticationService.GenerateServiceTokenAsync.
/// </summary>
public static class Token
{
    private static class ClaimTypes
    {
        public const string Email = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress";
        public const string NameIdentifier = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier";
        public const string Name = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name";
    }

    public static string GenerateInternalServiceToken(ApiConfig config)
    {
        var serviceEmail = config.ServiceEmail;
        var internalServiceAuth = config.InternalServiceAuth;

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(internalServiceAuth.SecretKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        // Claims (not Subject) so the payload keys are written verbatim, matching jsonwebtoken's
        // behaviour: ClaimsIdentity/Subject would otherwise run the outbound claim-type mapping
        // and rewrite short names like "email"/"name" into long .NET claim URIs.
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = internalServiceAuth.Issuer,
            Audience = internalServiceAuth.Audience,
            Expires = DateTime.UtcNow.AddMinutes(internalServiceAuth.TokenLifetimeMinutes),
            SigningCredentials = credentials,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = serviceEmail,
                ["email"] = serviceEmail,
                ["name"] = serviceEmail,
                ["service_type"] = "internal",
                [ClaimTypes.Email] = serviceEmail,
                [ClaimTypes.NameIdentifier] = serviceEmail,
                [ClaimTypes.Name] = serviceEmail,
            },
        };

        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(descriptor);
    }
}
