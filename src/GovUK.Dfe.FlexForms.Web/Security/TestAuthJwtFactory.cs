using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GovUK.Dfe.CoreLibs.Security.Configurations;
using Microsoft.IdentityModel.Tokens;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Mints JWTs for Test Authentication using the tenant (or host) TestAuthentication options,
/// so Web tokens validate against the API's per-tenant signing key without a restart.
/// </summary>
internal static class TestAuthJwtFactory
{
    private const int DefaultLifetimeMinutes = 60;

    public static string CreateToken(
        ClaimsPrincipal user,
        TestAuthenticationOptions options,
        int lifetimeMinutes = DefaultLifetimeMinutes)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.JwtSigningKey))
        {
            throw new InvalidOperationException(
                "TestAuthentication:JwtSigningKey is not configured for this tenant.");
        }

        if (string.IsNullOrWhiteSpace(options.JwtIssuer))
        {
            throw new InvalidOperationException(
                "TestAuthentication:JwtIssuer is not configured for this tenant.");
        }

        if (string.IsNullOrWhiteSpace(options.JwtAudience))
        {
            throw new InvalidOperationException(
                "TestAuthentication:JwtAudience is not configured for this tenant.");
        }

        var claims = user.Claims
            .Select(c => new Claim(c.Type, c.Value))
            .ToList();

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.JwtSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.JwtIssuer,
            audience: options.JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(lifetimeMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
