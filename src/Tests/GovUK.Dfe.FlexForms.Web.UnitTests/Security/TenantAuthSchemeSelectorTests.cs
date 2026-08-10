using GovUK.Dfe.CoreLibs.Security.Configurations;
using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Security;

public class TenantAuthSchemeSelectorTests
{
    [Theory]
    [InlineData("TestAuthentication", InteractiveAuthScheme.TestAuthentication)]
    [InlineData("Test", InteractiveAuthScheme.TestAuthentication)]
    [InlineData("EntraSso", InteractiveAuthScheme.EntraSso)]
    [InlineData("Entra", InteractiveAuthScheme.EntraSso)]
    [InlineData("DfESignIn", InteractiveAuthScheme.DfESignIn)]
    [InlineData("DSI", InteractiveAuthScheme.DfESignIn)]
    public void Resolve_UsesExplicitAuthenticationScheme(string scheme, InteractiveAuthScheme expected)
    {
        var httpContext = CreateHttpContext(new Dictionary<string, string?>
        {
            ["Authentication:Scheme"] = scheme,
            ["TestAuthentication:Enabled"] = "true",
            ["EntraSso:Enabled"] = "true"
        });

        var result = TenantAuthSchemeSelector.Resolve(httpContext);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Resolve_PrefersTest_WhenEnabledAndNoExplicitScheme()
    {
        var httpContext = CreateHttpContext(new Dictionary<string, string?>
        {
            ["TestAuthentication:Enabled"] = "true",
            ["EntraSso:Enabled"] = "true"
        });

        var result = TenantAuthSchemeSelector.Resolve(httpContext);

        Assert.Equal(InteractiveAuthScheme.TestAuthentication, result);
    }

    [Fact]
    public void Resolve_IgnoresExplicitTestScheme_InProduction()
    {
        var httpContext = CreateHttpContext(
            new Dictionary<string, string?>
            {
                ["Authentication:Scheme"] = "TestAuthentication",
                ["TestAuthentication:Enabled"] = "true",
                ["EntraSso:Enabled"] = "true"
            },
            environmentName: "Production");

        var result = TenantAuthSchemeSelector.Resolve(httpContext);

        Assert.Equal(InteractiveAuthScheme.EntraSso, result);
    }

    [Fact]
    public void Resolve_IgnoresEnabledTestAuth_InProdAlias()
    {
        var httpContext = CreateHttpContext(
            new Dictionary<string, string?>
            {
                ["TestAuthentication:Enabled"] = "true",
                ["EntraSso:Enabled"] = "false"
            },
            environmentName: "Prod");

        var result = TenantAuthSchemeSelector.Resolve(httpContext);

        Assert.Equal(InteractiveAuthScheme.DfESignIn, result);
    }

    [Fact]
    public void IsTestAuthenticationEnabled_ReturnsFalse_InProduction()
    {
        var httpContext = CreateHttpContext(
            new Dictionary<string, string?>
            {
                ["TestAuthentication:Enabled"] = "true"
            },
            environmentName: "Production");

        Assert.False(TenantAuthSchemeSelector.IsTestAuthenticationEnabled(httpContext));
    }

    [Fact]
    public void Resolve_UsesEntra_WhenOnlyEntraEnabled()
    {
        var httpContext = CreateHttpContext(new Dictionary<string, string?>
        {
            ["TestAuthentication:Enabled"] = "false",
            ["EntraSso:Enabled"] = "true"
        });

        var result = TenantAuthSchemeSelector.Resolve(httpContext);

        Assert.Equal(InteractiveAuthScheme.EntraSso, result);
    }

    [Fact]
    public void Resolve_FallsBackToHostTestOptions_WhenTenantConfigMissing()
    {
        var httpContext = CreateHttpContext(null);
        var hostTest = Options.Create(new TestAuthenticationOptions { Enabled = true });

        var result = TenantAuthSchemeSelector.Resolve(httpContext, hostTest);

        Assert.Equal(InteractiveAuthScheme.TestAuthentication, result);
    }

    [Fact]
    public void Resolve_DefaultsToDfESignIn()
    {
        var httpContext = CreateHttpContext(new Dictionary<string, string?>
        {
            ["TestAuthentication:Enabled"] = "false",
            ["EntraSso:Enabled"] = "false"
        });

        var result = TenantAuthSchemeSelector.Resolve(httpContext);

        Assert.Equal(InteractiveAuthScheme.DfESignIn, result);
    }

    private static DefaultHttpContext CreateHttpContext(
        Dictionary<string, string?>? tenantSettings,
        string environmentName = "Development")
    {
        var services = new ServiceCollection();
        IConfiguration? tenantConfiguration = null;
        if (tenantSettings is not null)
        {
            tenantConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(tenantSettings)
                .Build();
        }

        services.AddScoped<ITenantRequestContext>(_ => new TenantRequestContext
        {
            TenantId = Guid.Parse("11111111-1111-4111-8111-111111111111"),
            TenantConfiguration = tenantConfiguration
        });

        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment(environmentName));

        var provider = services.BuildServiceProvider();
        return new DefaultHttpContext { RequestServices = provider };
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
