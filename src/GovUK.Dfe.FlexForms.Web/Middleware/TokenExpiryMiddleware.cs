using GovUK.Dfe.CoreLibs.Security.Configurations;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;

namespace GovUK.Dfe.FlexForms.Web.Middleware
{
    [ExcludeFromCodeCoverage]
    public class TokenExpiryMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TokenExpiryMiddleware> _logger;
        private readonly IOptions<TestAuthenticationOptions> _testAuthOptions;
        private readonly IOptions<EntraSsoOptions> _entraSsoOptions;
        private static readonly TimeSpan ExpiryThreshold = TimeSpan.FromMinutes(10);

        public TokenExpiryMiddleware(
            RequestDelegate next, 
            ILogger<TokenExpiryMiddleware> logger,
            IOptions<TestAuthenticationOptions> testAuthOptions,
            IOptions<EntraSsoOptions> entraSsoOptions)
        {
            _next = next;
            _logger = logger;
            _testAuthOptions = testAuthOptions;
            _entraSsoOptions = entraSsoOptions;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";
            var requestPath = context.Request.Path;

            // Skip token expiry checks when test authentication is the active interactive scheme
            if (TenantAuthSchemeSelector.IsTestAuthenticationActive(
                    context,
                    _testAuthOptions,
                    _entraSsoOptions))
            {
                await _next(context);
                return;
            }

            var result = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            
            if (result.Succeeded)
            {
                var expiresUtc = result.Properties?.ExpiresUtc;
                
                if (expiresUtc.HasValue)
                {
                    var remaining = expiresUtc.Value - DateTimeOffset.UtcNow;
                    
                    if (remaining <= TimeSpan.Zero)
                    {
                        context.Response.Redirect("/Logout?reason=token_expired");
                        return;
                    }
                    else if (remaining <= ExpiryThreshold)
                    {
                        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        await context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme);
                        return;
                    }
                }
                else
                {
                    _logger.LogWarning(
                        ">>>>>>>>>> Authentication >>> TokenExpiryMiddleware: Authentication ticket for user {UserId} has no expiry time. This may indicate a configuration issue.", 
                        userId);
                }
            }
            else
            {
                _logger.LogWarning(">>>>>>>>>> Authentication >>> TokenExpiryMiddleware: Authentication failed for user {UserId} at path {Path}. Reason: {Failure}", 
                    userId, requestPath, result.Failure?.Message ?? "Unknown");
            }

            _logger.LogDebug(">>>>>>>>>> Authentication >>> TokenExpiryMiddleware: Proceeding to next middleware for user {UserId}", userId);
            await _next(context);
        }
    }

    [ExcludeFromCodeCoverage]
    public static class TokenExpiryMiddlewareExtensions
    {
        public static IApplicationBuilder UseTokenExpiryCheck(this IApplicationBuilder app)
        {
            return app.UseMiddleware<TokenExpiryMiddleware>();
        }
    }
}
