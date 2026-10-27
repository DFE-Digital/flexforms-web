using GovUK.Dfe.FlexForms.Web.Features;
using GovUK.Dfe.FlexForms.Web.Pages.Admin;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.Mvc;
using System.Reflection;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Features;

public class TenantFeatureDefinitionProviderTests
{
    [Theory]
    [InlineData(null, null, false)]
    [InlineData(null, "true", true)]
    [InlineData("true", null, true)]
    [InlineData("true", "false", false)]
    [InlineData("false", "true", true)]
    public async Task TemplateStudio_UsesTheTenantSetting_ThenAppsettings(string? hostValue, string? tenantValue, bool expected)
    {
        var featureManager = CreateFeatureManager(
            Settings(hostValue),
            tenantValue is null ? new Dictionary<string, string?>() : Settings(tenantValue));

        Assert.Equal(expected, await featureManager.IsEnabledAsync(FeatureFlags.TemplateStudio));
    }

    [Fact]
    public async Task TemplateStudio_UsesAppsettings_OutsideARequest()
    {
        var services = new ServiceCollection()
            .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor())
            .AddSingleton<IConfiguration>(Configuration(Settings("true")))
            .AddSingleton<IFeatureDefinitionProvider, TenantFeatureDefinitionProvider>();
        services.AddFeatureManagement();
        await using var provider = services.BuildServiceProvider();

        Assert.True(await provider.GetRequiredService<IFeatureManager>().IsEnabledAsync(FeatureFlags.TemplateStudio));
    }

    [Fact]
    public async Task TenantFlags_SupportMicrosoftFeatureFilterObjects()
    {
        var featureManager = CreateFeatureManager(
            new Dictionary<string, string?>(),
            new Dictionary<string, string?>
            {
                ["FeatureManagement:TemplateStudio:EnabledFor:0:Name"] = "AlwaysOn"
            });

        Assert.True(await featureManager.IsEnabledAsync(FeatureFlags.TemplateStudio));
    }

    [Theory]
    [InlineData(typeof(TemplateStudioModel))]
    [InlineData(typeof(TemplateStudioPreviewModel))]
    public void TemplateStudioPages_AreGatedByTheTemplateStudioFlag(Type pageModel)
    {
        var gate = pageModel.GetCustomAttribute<FeatureGateAttribute>();

        Assert.NotNull(gate);
        Assert.Equal([FeatureFlags.TemplateStudio], gate.Features);
    }

    private static IFeatureManager CreateFeatureManager(
        Dictionary<string, string?> hostSettings,
        Dictionary<string, string?> tenantSettings)
    {
        var requestServices = new ServiceCollection()
            .AddSingleton<ITenantRequestContext>(new TenantRequestContext { TenantConfiguration = Configuration(tenantSettings) })
            .BuildServiceProvider();
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { RequestServices = requestServices } };

        var services = new ServiceCollection()
            .AddSingleton<IHttpContextAccessor>(accessor)
            .AddSingleton<IConfiguration>(Configuration(hostSettings))
            .AddSingleton<IFeatureDefinitionProvider, TenantFeatureDefinitionProvider>();
        services.AddFeatureManagement();
        return services.BuildServiceProvider().GetRequiredService<IFeatureManager>();
    }

    private static Dictionary<string, string?> Settings(string? templateStudio) =>
        templateStudio is null
            ? new Dictionary<string, string?>()
            : new Dictionary<string, string?> { ["FeatureManagement:TemplateStudio"] = templateStudio };

    private static IConfiguration Configuration(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
}
