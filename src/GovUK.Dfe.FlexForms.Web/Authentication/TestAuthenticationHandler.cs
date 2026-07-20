using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace GovUK.Dfe.FlexForms.Web.Authentication;

[ExcludeFromCodeCoverage]
public class TestAuthenticationHandler : AuthenticationHandler<TestAuthenticationSchemeOptions>
{
    public const string SchemeName = "TestAuthentication";
    
    private static class SessionKeys
    {
        public const string Email = "TestAuth:Email";
        public const string Token = "TestAuth:Token";
    }

    private static class TokenNames
    {
        public const string IdToken = "id_token";
        public const string AccessToken = "access_token";
    }

    public TestAuthenticationHandler(
        IOptionsMonitor<TestAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var path = Context.Request.Path;

        var email = Context.Session.GetString(SessionKeys.Email);
        var token = Context.Session.GetString(SessionKeys.Token);

        Logger.LogDebug(
            "TestAuth session check for {Path}. HasEmail: {HasEmail}, HasToken: {HasToken}",
            path, !string.IsNullOrEmpty(email), !string.IsNullOrEmpty(token));

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
        {
            Logger.LogDebug("TestAuthentication session data missing for {Path}, returning NoResult", path);
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = CreateUserClaims(email);
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, CreateAuthenticationProperties(token), SchemeName);

        Logger.LogInformation(
            "TestAuthentication successful for {Email} on path {Path}",
            email, path);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var returnUrl = properties?.RedirectUri;
        var loginUrl = string.IsNullOrEmpty(returnUrl) 
            ? "/TestLogin" 
            : $"/TestLogin?returnUrl={Uri.EscapeDataString(returnUrl)}";
            

            
        Response.Redirect(loginUrl);
        return Task.CompletedTask;
    }

    private static IEnumerable<Claim> CreateUserClaims(string email)
    {
        return new[]
        {
            new Claim(ClaimTypes.Name, email),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.NameIdentifier, email),
            new Claim("sub", email),
            new Claim("email", email)
        };
    }

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

[ExcludeFromCodeCoverage]
public class TestAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
    // Options class for future extensibility
} 