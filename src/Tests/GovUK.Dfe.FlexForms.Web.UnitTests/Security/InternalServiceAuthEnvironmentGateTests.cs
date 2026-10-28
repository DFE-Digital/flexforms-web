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

    private static IHostEnvironment Env(string name)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(name);
        return environment;
    }
}
