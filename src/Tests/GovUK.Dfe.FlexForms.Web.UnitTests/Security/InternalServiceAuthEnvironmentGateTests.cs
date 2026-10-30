using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Security;

public class InternalServiceAuthEnvironmentGateTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Local")]
    [InlineData("Test")]
    public void IsAllowed_DefaultsOn_OutsideProduction(string environmentName)
    {
        Assert.True(InternalServiceAuthEnvironmentGate.IsAllowed(
            Env(environmentName),
            new ConfigurationBuilder().Build()));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Prod")]
    public void IsAllowed_DefaultsOff_InProduction(string environmentName)
    {
        Assert.False(InternalServiceAuthEnvironmentGate.IsAllowed(
            Env(environmentName),
            new ConfigurationBuilder().Build()));
    }

    [Fact]
    public void IsAllowed_CanEnableInProduction_ViaConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [InternalServiceAuthEnvironmentGate.WebEdgeEnabledKey] = "true"
            })
            .Build();

        Assert.True(InternalServiceAuthEnvironmentGate.IsAllowed(Env("Production"), configuration));
    }

    [Fact]
    public void IsAllowed_DefaultsOn_WhenEnvironmentIsNull()
        => Assert.True(InternalServiceAuthEnvironmentGate.IsAllowed(null, new ConfigurationBuilder().Build()));

    [Fact]
    public void IsAllowed_CanDisableOutsideProduction_ViaConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [InternalServiceAuthEnvironmentGate.WebEdgeEnabledKey] = "false"
            })
            .Build();

        Assert.False(InternalServiceAuthEnvironmentGate.IsAllowed(Env("Development"), configuration));
    }

    [Fact]
    public void IsAllowed_UsesTenantFlag_InProduction()
    {
        Assert.True(InternalServiceAuthEnvironmentGate.IsAllowed(
            Env("Production"),
            new ConfigurationBuilder().Build(),
            Tenant(enabled: true)));
        Assert.False(InternalServiceAuthEnvironmentGate.IsAllowed(
            Env("Production"),
            new ConfigurationBuilder().Build(),
            Tenant(enabled: false)));
    }

    [Fact]
    public void IsAllowed_HostFalse_OverridesTenantTrue()
    {
        var host = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [InternalServiceAuthEnvironmentGate.WebEdgeEnabledKey] = "false"
            })
            .Build();

        Assert.False(InternalServiceAuthEnvironmentGate.IsAllowed(
            Env("Production"),
            host,
            Tenant(enabled: true)));
    }

    [Fact]
    public void IsAllowed_TenantFalse_DisablesOutsideProduction()
    {
        Assert.False(InternalServiceAuthEnvironmentGate.IsAllowed(
            Env("Development"),
            new ConfigurationBuilder().Build(),
            Tenant(enabled: false)));
    }

    private static IConfiguration Tenant(bool enabled)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [InternalServiceAuthEnvironmentGate.WebEdgeEnabledKey] = enabled ? "true" : "false"
            })
            .Build();

    private static IHostEnvironment Env(string name)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(name);
        return environment;
    }
}
