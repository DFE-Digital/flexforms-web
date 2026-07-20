using GovUK.Dfe.FlexForms.Web.Configuration;
using GovUK.Dfe.FlexForms.Web.Middleware;
using GovUK.Dfe.FlexForms.Web.Services.Platform;
using GovUK.Dfe.FlexForms.Web.Services.Tenant;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.Extensions.Configuration;

namespace GovUK.Dfe.FlexForms.Web.Extensions;

/// <summary>
/// Registers platform bootstrap and per-request tenant configuration services.
/// </summary>
public static class PlatformBootstrapExtensions
{
    public static IServiceCollection AddPlatformTenantConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PlatformBootstrapOptions>(configuration.GetSection(PlatformBootstrapOptions.SectionName));

        var bootstrap = configuration.GetSection(PlatformBootstrapOptions.SectionName).Get<PlatformBootstrapOptions>();
        if (bootstrap is not { Enabled: true })
        {
            services.AddScoped<ITenantRequestContext, TenantRequestContext>();
            return services;
        }

        // Do not use ConfigureHttpClientDefaults here: host bootstrap resolves this
        // client before an HTTP request exists. Business API clients get X-Tenant-ID
        // from HeaderForwardingHandler via TenantApiClientSettingsProvider.
        services.AddHttpClient<PlatformConfigurationApiClient>();
        services.AddSingleton<IPlatformAccessTokenProvider, PlatformAccessTokenProvider>();
        services.AddSingleton<ITenantConfigurationCache, TenantConfigurationCache>();
        services.AddScoped<TenantConfigurationLoader>();
        services.AddScoped<ITenantIdResolver, TenantIdResolver>();
        services.AddScoped<PlatformHostConfigurationBootstrapper>();
        services.AddScoped<ITenantRequestContext, TenantRequestContext>();

        return services;
    }

    /// <summary>
    /// Loads host configuration from the platform API and merges it into the application configuration.
    /// Call before <see cref="WebApplicationBuilder.Build"/>.
    /// </summary>
    public static async Task BootstrapPlatformHostConfigurationAsync(this WebApplicationBuilder builder)
    {
        var bootstrap = builder.Configuration
            .GetSection(PlatformBootstrapOptions.SectionName)
            .Get<PlatformBootstrapOptions>();

        if (bootstrap is not { Enabled: true })
        {
            return;
        }

        using var scope = builder.Services.BuildServiceProvider().CreateScope();
        var hostBootstrapper = scope.ServiceProvider.GetRequiredService<PlatformHostConfigurationBootstrapper>();
        var hostConfiguration = await hostBootstrapper.LoadHostConfigurationAsync();

        builder.Configuration.AddInMemoryCollection(hostConfiguration);
    }

    public static IApplicationBuilder UsePlatformTenantConfiguration(this IApplicationBuilder app)
    {
        return app.UseMiddleware<TenantConfigurationMiddleware>();
    }
}
