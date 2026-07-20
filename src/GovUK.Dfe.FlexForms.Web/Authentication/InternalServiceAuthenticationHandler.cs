using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using GovUK.Dfe.FlexForms.Web.Services;

namespace GovUK.Dfe.FlexForms.Web.Authentication;

/// <summary>
/// Authentication handler for internal service authentication
/// Follows the same pattern as TestAuthenticationHandler but reads from headers instead of session
/// Implements forwarder pattern - only engages when service headers are present
/// </summary>
public class InternalServiceAuthenticationHandler(
    IOptionsMonitor<InternalServiceAuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ISystemClock clock,
    IInternalServiceAuthenticationService internalServiceAuth) : AuthenticationHandler<InternalServiceAuthenticationSchemeOptions>(options, logger, encoder, clock)
{
    private static class SessionKeys
    {
        public const string Email = "InternalAuth:Email";
        public const string Token = "InternalAuth:Token";
    }

    public const string SchemeName = "InternalServiceAuth";
    
    private static class HeaderNames
    {
        public const string ServiceEmail = "x-service-email";
        public const string ServiceApiKey = "x-service-api-key";
    }
    
    private static class TokenNames
    {
        public const string IdToken = "id_token";
        public const string AccessToken = "access_token";
    }
    
    private readonly IInternalServiceAuthenticationService _internalServiceAuth = internalServiceAuth;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var path = Context.Request.Path;
        
        // FORWARDER PATTERN: Only engage this handler if the service header is present
        // This is a performance optimization - avoid unnecessary processing
        var serviceEmail = Context.Request.Headers[HeaderNames.ServiceEmail].FirstOrDefault();
        if (string.IsNullOrEmpty(serviceEmail))
        {
            // No service header = not an internal service request
            // Return NoResult immediately without logging (better performance)
            return AuthenticateResult.NoResult();
        }

        Logger.LogDebug("InternalServiceAuth checking request for {Email} on {Path}", serviceEmail, path);

        // Get API key from headers
        var apiKey = Context.Request.Headers[HeaderNames.ServiceApiKey].FirstOrDefault();

        // Validate required headers are present
        if (string.IsNullOrEmpty(apiKey))
        {
            Logger.LogWarning(
                "InternalServiceAuth missing API key for {Email}",
                serviceEmail);
            return AuthenticateResult.Fail("Missing X-Service-Api-Key header");
        }

        // SECURITY: Validate service credentials (email + API key)
        if (!_internalServiceAuth.ValidateServiceCredentials(serviceEmail, apiKey))
        {
            Logger.LogWarning(
                "Unauthorized service authentication attempt: {Email} from {IP}", 
                serviceEmail, 
                Context.Connection.RemoteIpAddress);
            return AuthenticateResult.Fail("Invalid service credentials");
        }

        // Create short-lived token automatically (no manual endpoint needed)
        var token = await _internalServiceAuth.GenerateServiceTokenAsync(serviceEmail);

        Context.Session.SetString(InternalServiceAuthenticationHandler.SessionKeys.Email, serviceEmail);
        Context.Session.SetString(InternalServiceAuthenticationHandler.SessionKeys.Token, token);

        // Create claims and ticket (exactly like Test Auth)
        var claims = CreateServiceClaims(serviceEmail);
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, CreateAuthenticationProperties(token), SchemeName);

        Logger.LogInformation(
            "InternalServiceAuth successful for {Email} on path {Path}",
            serviceEmail, path);

        return AuthenticateResult.Success(ticket);
    }

    // Exactly like Test Auth
    private static IEnumerable<Claim> CreateServiceClaims(string serviceEmail)
    {
        return new[]
        {
            new Claim(ClaimTypes.Name, serviceEmail),
            new Claim(ClaimTypes.Email, serviceEmail),
            new Claim(ClaimTypes.NameIdentifier, serviceEmail),
            new Claim("sub", serviceEmail),
            new Claim("email", serviceEmail),
            new Claim("service_type", "internal")
        };
    }

    // Exactly like Test Auth
    private static AuthenticationProperties CreateAuthenticationProperties(string token)
    {
        var properties = new AuthenticationProperties();
        properties.StoreTokens(new[]
        {
            new AuthenticationToken { Name = TokenNames.IdToken, Value = token },
            new AuthenticationToken { Name = TokenNames.AccessToken, Value = token }
        });
        return properties;
    }
}

/// <summary>
/// Options for internal service authentication scheme
/// </summary>
public class InternalServiceAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
    // Options class for future extensibility
}

