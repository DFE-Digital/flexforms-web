using GovUK.Dfe.FlexForms.Web.Tenancy;
using GovUK.Dfe.CoreLibs.Security.TokenRefresh.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.Configuration;

/// <summary>
/// Resolves token refresh options from tenant configuration for the current request.
/// Uses <see cref="IHttpContextAccessor"/> so options remain correct outside a captured DI scope.
/// </summary>
public sealed class TenantTokenRefreshOptionsAccessor(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration hostConfiguration) : IOptions<TokenRefreshOptions>
{
    /// <inheritdoc />
    public TokenRefreshOptions Value => Build();

    private TokenRefreshOptions Build()
    {
        var tenantRequestContext = httpContextAccessor.HttpContext?.RequestServices
            .GetService<ITenantRequestContext>();
        var config = tenantRequestContext?.TenantConfiguration ?? hostConfiguration;

        var options = new TokenRefreshOptions();
        config.GetSection("TokenRefresh").Bind(options);

        var oidcSection = config.GetSection("DfESignIn");
        if (oidcSection.Exists())
        {
            if (string.IsNullOrWhiteSpace(options.ClientId))
            {
                options.ClientId = oidcSection["ClientId"];
            }

            if (string.IsNullOrWhiteSpace(options.ClientSecret))
            {
                options.ClientSecret = oidcSection["ClientSecret"];
            }

            var authority = oidcSection["Authority"];
            if (!string.IsNullOrWhiteSpace(authority))
            {
                var authorityUri = authority.TrimEnd('/');
                options.TokenEndpoint ??= $"{authorityUri}/token";
                options.IntrospectionEndpoint ??= $"{authorityUri}/token/introspection";
            }
        }

        options.Validate();
        return options;
    }
}
