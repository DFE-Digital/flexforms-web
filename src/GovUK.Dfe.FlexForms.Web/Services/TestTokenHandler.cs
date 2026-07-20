using GovUK.Dfe.CoreLibs.Security.Configurations;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;

namespace GovUK.Dfe.FlexForms.Web.Services;

[ExcludeFromCodeCoverage]
public class TestTokenHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TestAuthenticationOptions _options;
    private readonly ILogger<TestTokenHandler> _logger;

    private static class SessionKeys
    {
        public const string Token = "TestAuth:Token";
    }

    public TestTokenHandler(
        IHttpContextAccessor httpContextAccessor,
        IOptions<TestAuthenticationOptions> options,
        ILogger<TestTokenHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";
        
        // Only modify requests if test authentication is enabled
        if (_options.Enabled && _httpContextAccessor.HttpContext is not null)
        {
            var testToken = _httpContextAccessor.HttpContext.Session.GetString(SessionKeys.Token);

            if (!string.IsNullOrEmpty(testToken))
            {
                // Replace the authorization header with the test token
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", testToken);
            }
            else
            {
            }
        }
        else
        {
        }

        var response = await base.SendAsync(request, cancellationToken);
            
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
        }

        return response;
    }
} 