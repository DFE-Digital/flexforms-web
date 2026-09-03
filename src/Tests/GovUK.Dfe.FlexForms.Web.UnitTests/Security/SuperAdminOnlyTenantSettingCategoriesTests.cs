using GovUK.Dfe.FlexForms.Web.Security;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Security;

public class SuperAdminOnlyTenantSettingCategoriesTests
{
    [Theory]
    [InlineData("ApplicationInsights")]
    [InlineData("ConnectionStrings")]
    [InlineData("FileStorage")]
    [InlineData("Email")]
    public void IsRestricted_ShouldBeTrue_ForInfrastructureCategories(string category)
    {
        Assert.True(SuperAdminOnlyTenantSettingCategories.IsRestricted(category));
    }

    [Fact]
    public void IsRestricted_ShouldBeFalse_ForLayout()
    {
        Assert.False(SuperAdminOnlyTenantSettingCategories.IsRestricted("Layout"));
    }
}
