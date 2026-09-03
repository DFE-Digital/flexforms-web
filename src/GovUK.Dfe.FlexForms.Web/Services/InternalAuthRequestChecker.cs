using System.Security.Cryptography;
using System.Text;
using GovUK.Dfe.CoreLibs.Security.Interfaces;

namespace GovUK.Dfe.FlexForms.Web.Services;

/// <summary>
/// Validates inbound InternalServiceAuth headers against the current tenant's
/// <c>InternalServiceAuth:Services</c> configuration (with host fallback).
/// </summary>
public sealed class InternalAuthRequestChecker(
    IInternalServiceAuthOptionsResolver optionsResolver,
    ILogger<InternalAuthRequestChecker> logger) : ICustomRequestChecker
{
    private const string ServiceEmailHeaderKey = "x-service-email";
    private const string ServiceApiHeaderKey = "x-service-api-key";

    /// <inheritdoc />
    public bool IsValidRequest(HttpContext httpContext)
    {
        var serviceEmail = httpContext.Request.Headers[ServiceEmailHeaderKey].ToString();
        var serviceApiKey = httpContext.Request.Headers[ServiceApiHeaderKey].ToString();
        if (string.IsNullOrWhiteSpace(serviceEmail) || string.IsNullOrWhiteSpace(serviceApiKey))
        {
            return false;
        }

        var config = optionsResolver.Resolve();

        var serviceConfig = config.Services
            .FirstOrDefault(s => s.Email.Equals(serviceEmail, StringComparison.OrdinalIgnoreCase));

        if (serviceConfig == null)
        {
            logger.LogDebug("Service email not found in InternalServiceAuth configuration: {Email}", serviceEmail);
            return false;
        }

        var isValid = ConstantTimeEquals(serviceConfig.ApiKey, serviceApiKey);

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
