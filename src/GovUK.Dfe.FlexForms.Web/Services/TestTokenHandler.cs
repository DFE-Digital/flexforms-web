using GovUK.Dfe.CoreLibs.Security.Configurations;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;

namespace GovUK.Dfe.FlexForms.Web.Services;

[ExcludeFromCodeCoverage]
public class TestTokenHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOptions<TestAuthenticationOptions> _options;
    private readonly IOptions<EntraSsoOptions> _entraSsoOptions;
    private readonly ILogger<TestTokenHandler> _logger;

    private static class SessionKeys
    {
        public const string Token = "TestAuth:Token";
    }

    public TestTokenHandler(
        IHttpContextAccessor httpContextAccessor,
        IOptions<TestAuthenticationOptions> options,
        IOptions<EntraSsoOptions> entraSsoOptions,
        ILogger<TestTokenHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _options = options;
        _entraSsoOptions = entraSsoOptions;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null
            && TenantAuthSchemeSelector.IsTestAuthenticationActive(
                httpContext,
                _options,
                _entraSsoOptions))
        {
            var testToken = httpContext.Session.GetString(SessionKeys.Token);

            if (!string.IsNullOrEmpty(testToken))
            {
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", testToken);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
