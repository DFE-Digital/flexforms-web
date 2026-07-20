using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.Configuration;

/// <summary>
/// Resolves <see cref="IOptions{TOptions}"/> from host configuration with a per-request tenant overlay.
/// Uses <see cref="IHttpContextAccessor"/> so values remain correct when resolved outside a request DI scope.
/// </summary>
/// <typeparam name="TOptions">The options type to bind.</typeparam>
public sealed class TenantOptionsAccessor<TOptions>(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration hostConfiguration,
    string sectionName) : IOptions<TOptions>
    where TOptions : class, new()
{
    /// <inheritdoc />
    public TOptions Value
    {
        get
        {
            var tenantContext = httpContextAccessor.HttpContext?.RequestServices
                .GetService<ITenantRequestContext>();
            var tenantSection = tenantContext?.TenantConfiguration?.GetSection(sectionName);

            // Prefer tenant section when present. Binding host then tenant appends list items
            // (e.g. InternalServiceAuth:Services), which breaks per-tenant credential isolation.
            if (tenantSection?.Exists() == true && tenantSection.GetChildren().Any())
            {
                var tenantOptions = new TOptions();
                tenantSection.Bind(tenantOptions);
                return tenantOptions;
            }

            var hostOptions = new TOptions();
            hostConfiguration.GetSection(sectionName).Bind(hostOptions);
            return hostOptions;
        }
    }
}
