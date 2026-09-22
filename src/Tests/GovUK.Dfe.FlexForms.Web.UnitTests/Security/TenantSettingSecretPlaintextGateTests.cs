using GovUK.Dfe.FlexForms.Web.Security;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Security;

public class TenantSettingSecretPlaintextGateTests
{
    [Theory]
    [InlineData("Development", true)]
    [InlineData("Test", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    [InlineData(null, false)]
    public void IsPlaintextEnvironment_ShouldMatchExpected(string? environmentName, bool expected)
    {
        Assert.Equal(expected, TenantSettingSecretPlaintextGate.IsPlaintextEnvironment(environmentName));
    }
}
