using GovUK.Dfe.CoreLibs.Security.Configurations;
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
        private readonly TestAuthenticationOptions _testAuthOptions;
        private static readonly TimeSpan ExpiryThreshold = TimeSpan.FromMinutes(10);

        public TokenExpiryMiddleware(
            RequestDelegate next, 
            ILogger<TokenExpiryMiddleware> logger,
            IOptions<TestAuthenticationOptions> testAuthOptions)
        {
            _next = next;
            _logger = logger;
            _testAuthOptions = testAuthOptions.Value;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";
            var requestPath = context.Request.Path;
            var userAgent = context.Request.Headers["User-Agent"].FirstOrDefault() ?? "Unknown";
            

            
            // Skip token expiry checks when test authentication is enabled
            if (_testAuthOptions.Enabled)
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
                    
                    // Log token status for debugging

                    
                    if (remaining <= TimeSpan.Zero)
                    {


                        // Token already expired - force logout immediately
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