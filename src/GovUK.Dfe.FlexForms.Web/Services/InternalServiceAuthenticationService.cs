using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GovUK.Dfe.CoreLibs.Security.Authorization;
using GovUK.Dfe.CoreLibs.Security.Configurations;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.Services;

/// <summary>
/// Authenticates internal services using per-tenant InternalServiceAuth credentials
/// and mints a short-lived HMAC JWT with that tenant's SecretKey/Issuer/Audience.
/// </summary>
public sealed class InternalServiceAuthenticationService(
    IInternalServiceAuthOptionsResolver optionsResolver,
    ILoggerFactory loggerFactory,
    ILogger<InternalServiceAuthenticationService> logger) : IInternalServiceAuthenticationService
{
    /// <inheritdoc />
    public bool ValidateServiceCredentials(string serviceEmail, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(serviceEmail) || string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogDebug("Service credentials validation failed: empty email or API key");
            return false;
        }

        var config = optionsResolver.Resolve();
        var serviceConfig = config.Services
            .FirstOrDefault(s => s.Email.Equals(serviceEmail, StringComparison.OrdinalIgnoreCase));

        if (serviceConfig == null)
        {
            logger.LogDebug("Service email not found in configuration: {Email}", serviceEmail);
            return false;
        }

        var isValid = ConstantTimeEquals(serviceConfig.ApiKey, apiKey);

        if (!isValid)
        {
            logger.LogWarning("Invalid API key provided for service: {Email}", serviceEmail);
        }
        else
        {
            logger.LogDebug("Service credentials validated successfully for: {Email}", serviceEmail);
        }

        return isValid;
    }

    /// <inheritdoc />
    public async Task<string> GenerateServiceTokenAsync(string serviceEmail)
    {
        var config = optionsResolver.Resolve();

        if (string.IsNullOrWhiteSpace(config.SecretKey))
        {
            throw new InvalidOperationException(
                "InternalServiceAuth:SecretKey is not configured for the current tenant (or host fallback).");
        }

        logger.LogInformation("Generating InternalServiceAuth token for service: {Email}", serviceEmail);

        var claims = CreateServiceClaims(serviceEmail);
        var identity = new ClaimsIdentity(claims, "InternalServiceAuth");
        var principal = new ClaimsPrincipal(identity);

        // Mint with the resolved tenant (or host) TokenSettings so API exchange can validate
        // against the same tenant InternalServiceAuth SecretKey/Issuer/Audience.
        var tokenSettings = new TokenSettings
        {
            SecretKey = config.SecretKey,
            Issuer = config.Issuer,
            Audience = config.Audience,
            TokenLifetimeMinutes = config.TokenLifetimeMinutes > 0 ? config.TokenLifetimeMinutes : 10,
            BufferInSeconds = config.BufferInSeconds
        };

        var tokenService = new UserTokenService(
            Options.Create(tokenSettings),
            loggerFactory.CreateLogger<UserTokenService>());

        var token = await tokenService.GetUserTokenAsync(principal);

        logger.LogDebug("Token generated successfully for {Email}", serviceEmail);
        return token;
    }

    private static IEnumerable<Claim> CreateServiceClaims(string serviceEmail)
    {
        return
        [
            new Claim(ClaimTypes.Email, serviceEmail),
            new Claim(ClaimTypes.NameIdentifier, serviceEmail),
            new Claim(ClaimTypes.Name, serviceEmail),
            new Claim("sub", serviceEmail),
            new Claim("email", serviceEmail),
            new Claim("service_type", "internal"),
            new Claim("exp", DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds().ToString())
        ];
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a == null || b == null)
            return false;

        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);

        if (aBytes.Length != bBytes.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
