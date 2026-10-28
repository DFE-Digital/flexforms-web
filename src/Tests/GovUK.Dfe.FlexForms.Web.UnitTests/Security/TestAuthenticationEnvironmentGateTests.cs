using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Security;

public class TestAuthenticationEnvironmentGateTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Local")]
    [InlineData("Test")]
    public void IsAllowed_ReturnsTrue_OnAllowList(string environmentName)
        => Assert.True(TestAuthenticationEnvironmentGate.IsAllowed(environmentName));

    [Theory]
    [InlineData("Production")]
    [InlineData("Prod")]
    [InlineData("Staging")]
    [InlineData(null)]
    [InlineData("")]
    public void IsAllowed_ReturnsFalse_OffAllowList(string? environmentName)
        => Assert.False(TestAuthenticationEnvironmentGate.IsAllowed(environmentName));

    [Fact]
    public void IsAllowed_ReturnsFalse_WhenHostEnvironmentIsNull()
        => Assert.False(TestAuthenticationEnvironmentGate.IsAllowed((IHostEnvironment?)null));

    [Fact]
    public void IsProduction_ReturnsTrue_ForProductionHostEnvironment()
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(Environments.Production);

        Assert.True(TestAuthenticationEnvironmentGate.IsProduction(env));
        Assert.False(TestAuthenticationEnvironmentGate.IsAllowed(env));
    }

    [Fact]
    public void IsProduction_ReturnsTrue_ForProdAlias()
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Prod");

        Assert.True(TestAuthenticationEnvironmentGate.IsProduction(env));
    }

    [Fact]
    public void IsProduction_ReturnsFalse_WhenHostEnvironmentIsNull()
        => Assert.False(TestAuthenticationEnvironmentGate.IsProduction(null));

    [Fact]
    public void IsProduction_ReturnsFalse_WhenEnvironmentNameIsMissing()
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns((string?)null);

        Assert.False(TestAuthenticationEnvironmentGate.IsProduction(env));
    }
}
