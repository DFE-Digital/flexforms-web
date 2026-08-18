using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Tenancy;

public class TenantApiClientSettingsProviderTests
{
    [Fact]
    public void GetSettings_ShouldBindFromTenantConfigurationAndSetTenantId()
    {
        var tenantId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var tenantConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ExternalApplicationsApiClient:BaseUrl"] = "https://api.example/",
                ["ExternalApplicationsApiClient:ClientId"] = "client-id",
                ["ExternalApplicationsApiClient:Scope"] = "api://scope/.default"
            })
            .Build();

        var tenantContext = new TenantRequestContext
        {
            TenantId = tenantId,
            TenantConfiguration = tenantConfiguration
        };

        var services = new ServiceCollection();
        services.AddScoped<ITenantRequestContext>(_ => tenantContext);
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = provider
        };
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        var hostConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ExternalApplicationsApiClient:BaseUrl"] = "https://host.example/"
            })
            .Build();

        var settingsProvider = new TenantApiClientSettingsProvider(httpContextAccessor, hostConfiguration);

        var settings = settingsProvider.GetSettings();

        Assert.Equal("https://api.example/", settings.BaseUrl);
        Assert.Equal("client-id", settings.ClientId);
        Assert.Equal(tenantId, settings.TenantId);
    }

    [Fact]
    public void GetSettings_ShouldUseAmbientTenant_WhenHttpContextMissing()
    {
        var tenantId = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var tenantContext = new TenantRequestContext { TenantId = tenantId };

        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        var hostConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ExternalApplicationsApiClient:BaseUrl"] = "https://host.example/"
            })
            .Build();

        var settingsProvider = new TenantApiClientSettingsProvider(httpContextAccessor, hostConfiguration);

        using (AmbientTenantRequestContext.Use(tenantContext))
        {
            var settings = settingsProvider.GetSettings();
            Assert.Equal("https://host.example/", settings.BaseUrl);
            Assert.Equal(tenantId, settings.TenantId);
        }
    }

    [Fact]
    public void GetSettings_ShouldFallBackToHost_WhenTenantConfigurationMissing()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        var hostConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ExternalApplicationsApiClient:BaseUrl"] = "https://host.example/",
                ["ExternalApplicationsApiClient:ClientId"] = "host-client"
            })
            .Build();

        var settingsProvider = new TenantApiClientSettingsProvider(httpContextAccessor, hostConfiguration);

        var settings = settingsProvider.GetSettings();

        Assert.Equal("https://host.example/", settings.BaseUrl);
        Assert.Equal("host-client", settings.ClientId);
    }
}
