using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Options;
using GovUK.Dfe.FlexForms.Web.Configuration;
using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Web.Services;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using GovUK.Dfe.FlexForms.Api.Client.Settings;
using GovUK.Dfe.CoreLibs.Security.Configurations;
using GovUK.Dfe.CoreLibs.Security.TokenRefresh.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.Extensions;

/// <summary>
/// Registers tenant-aware platform bootstrap services for API clients and authentication.
/// </summary>
public static class TenantAwareServiceCollectionExtensions
{
    /// <summary>
    /// Registers request-scoped app configuration and, when platform bootstrap is enabled,
    /// tenant-aware API client / token-refresh settings.
    /// </summary>
    public static IServiceCollection AddTenantAwarePlatformServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<TenantAppConfiguration>();
        services.AddScoped<ITenantAppConfiguration>(sp => sp.GetRequiredService<TenantAppConfiguration>());
        services.AddScoped<IRequestAppConfiguration>(sp => sp.GetRequiredService<TenantAppConfiguration>());
        services.AddSingleton<IInternalServiceAuthOptionsResolver, InternalServiceAuthOptionsResolver>();

        var bootstrap = configuration.GetSection(PlatformBootstrapOptions.SectionName).Get<PlatformBootstrapOptions>();
        if (bootstrap is not { Enabled: true })
        {
            return services;
        }

        services.AddSingleton<IApiClientSettingsProvider, TenantApiClientSettingsProvider>();
        services.AddSingleton<IOptions<TokenRefreshOptions>, TenantTokenRefreshOptionsAccessor>();

        return services;
    }

    /// <summary>
    /// Replaces host <see cref="IOptions{TOptions}"/> registrations with tenant-aware accessors.
    /// Call after <c>services.Configure&lt;TOptions&gt;</c> so these registrations win.
    /// </summary>
    public static IServiceCollection AddTenantAwareOptionsAccessors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var bootstrap = configuration.GetSection(PlatformBootstrapOptions.SectionName).Get<PlatformBootstrapOptions>();
        if (bootstrap is not { Enabled: true })
        {
            return services;
        }

        AddTenantOptions<ApplicationTerminologyOptions>(services, "ApplicationTerminology");
        AddTenantOptions<NotificationBannerOptions>(services, "NotificationBanner");
        AddTenantOptions<DashboardOptions>(services, "Dashboard");
        AddTenantOptions<ApplicationSubmissionOptions>(services, "ApplicationSubmission");
        AddTenantOptions<TokenRefreshSettings>(services, "TokenRefresh");
        AddTenantOptions<InternalServiceAuthOptions>(services, InternalServiceAuthOptions.SectionName);
        AddTenantOptions<TestAuthenticationOptions>(services, TestAuthenticationOptions.SectionName);

        return services;
    }

    private static void AddTenantOptions<TOptions>(IServiceCollection services, string sectionName)
        where TOptions : class, new()
    {
        services.AddSingleton<IOptions<TOptions>>(sp =>
            new TenantOptionsAccessor<TOptions>(
                sp.GetRequiredService<IHttpContextAccessor>(),
                sp.GetRequiredService<IConfiguration>(),
                sectionName));
    }
}
