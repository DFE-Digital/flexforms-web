namespace GovUK.Dfe.FlexForms.Web.Services;

/// <summary>
/// Service for authenticating internal services using email-based validation and token generation
/// </summary>
public interface IInternalServiceAuthenticationService
{
    /// <summary>
    /// Validates service email and API key combination
    /// Uses constant-time comparison to prevent timing attacks
    /// </summary>
    /// <param name="serviceEmail">The service email identifier</param>
    /// <param name="apiKey">The shared API key</param>
    /// <returns>True if the credentials are valid, otherwise false</returns>
    bool ValidateServiceCredentials(string serviceEmail, string apiKey);
    
    /// <summary>
    /// Generates a short-lived (5 minute) authentication token for an internal service
    /// </summary>
    /// <param name="serviceEmail">The service email to generate token for</param>
    /// <returns>A task containing the generated token</returns>
    Task<string> GenerateServiceTokenAsync(string serviceEmail);
}

