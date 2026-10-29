using GovUK.Dfe.FlexForms.Web.Middleware;
using GovUK.Dfe.FlexForms.Web.Tenancy;
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
        public const string TenantId = "TestAuth:TenantId";
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
        var sessionTenant = Context.Session.GetString(SessionKeys.TenantId);

        Logger.LogDebug(
            "TestAuth session check for {Path}. HasEmail: {HasEmail}, HasToken: {HasToken}",
            path, !string.IsNullOrEmpty(email), !string.IsNullOrEmpty(token));

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
        {
            Logger.LogDebug("TestAuthentication session data missing for {Path}, returning NoResult", path);
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var resolvedTenant = Context.RequestServices.GetService<ITenantRequestContext>()?.TenantId;
        if (Guid.TryParse(sessionTenant, out var boundTenant)
            && resolvedTenant is Guid currentTenant
            && boundTenant != currentTenant)
        {
            Logger.LogWarning(
                "TestAuthentication tenant mismatch for {Email}: session {SessionTenant} vs host {ResolvedTenant}",
                email,
                boundTenant,
                currentTenant);
            Context.Session.Remove(SessionKeys.Email);
            Context.Session.Remove(SessionKeys.Token);
            Context.Session.Remove(SessionKeys.TenantId);
            return Task.FromResult(AuthenticateResult.Fail("Test authentication tenant mismatch."));
        }

        var claims = CreateUserClaims(email, sessionTenant);
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

    private static IEnumerable<Claim> CreateUserClaims(string email, string? sessionTenant)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, email),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.NameIdentifier, email),
            new("sub", email),
            new("email", email)
        };

        if (Guid.TryParse(sessionTenant, out _))
            claims.Add(new Claim(TenantSessionBindingMiddleware.TenantIdClaimType, sessionTenant!));

        return claims;
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