using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.FeatureManagement;

namespace GovUK.Dfe.FlexForms.Web.Features;

/// <summary>
/// Reads feature flags from the current tenant's <c>FeatureManagement</c> setting, falling back to the host
/// appsettings for any feature the tenant doesn't set. Both use the Microsoft.FeatureManagement configuration
/// format, so a flag can be a plain boolean or an object with feature filters.
/// </summary>
public sealed class TenantFeatureDefinitionProvider(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration hostConfiguration) : IFeatureDefinitionProvider, IDisposable
{
    private const string Section = "FeatureManagement";
    private static readonly object TenantProviderKey = new();

    private readonly ConfigurationFeatureDefinitionProvider _hostProvider = new(hostConfiguration);

    public Task<FeatureDefinition> GetFeatureDefinitionAsync(string featureName)
    {
        var tenantProvider = TenantProvider();
        return tenantProvider is not null && tenantProvider.Configuration.GetSection($"{Section}:{featureName}").Exists()
            ? tenantProvider.Provider.GetFeatureDefinitionAsync(featureName)
            : _hostProvider.GetFeatureDefinitionAsync(featureName);
    }

    public async IAsyncEnumerable<FeatureDefinition> GetAllFeatureDefinitionsAsync()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tenantProvider = TenantProvider();
        if (tenantProvider is not null)
        {
            await foreach (var definition in tenantProvider.Provider.GetAllFeatureDefinitionsAsync())
            {
                seen.Add(definition.Name);
                yield return definition;
            }
        }

        await foreach (var definition in _hostProvider.GetAllFeatureDefinitionsAsync())
        {
            if (seen.Add(definition.Name))
                yield return definition;
        }
    }

    public void Dispose() => _hostProvider.Dispose();

    /// <summary>
    /// The tenant configuration is rebuilt for every request, so its provider is created once per request.
    /// </summary>
    private TenantProviderEntry? TenantProvider()
    {
        var context = httpContextAccessor.HttpContext;
        var configuration = context?.RequestServices.GetService<ITenantRequestContext>()?.TenantConfiguration;
        if (context is null || configuration is null)
            return null;

        if (context.Items[TenantProviderKey] is TenantProviderEntry existing
            && ReferenceEquals(existing.Configuration, configuration))
        {
            return existing;
        }

        var provider = new ConfigurationFeatureDefinitionProvider(configuration);
        context.Response.RegisterForDispose(provider);
        var entry = new TenantProviderEntry(configuration, provider);
        context.Items[TenantProviderKey] = entry;
        return entry;
    }

    private sealed record TenantProviderEntry(IConfiguration Configuration, ConfigurationFeatureDefinitionProvider Provider);
}
