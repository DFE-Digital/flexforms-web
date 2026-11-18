using GovUK.Dfe.FlexForms.Application.Contributors;

namespace GovUK.Dfe.FlexForms.Application.Tests.Contributors;

public class ContributorInviteDefaultAccessTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsFullEditByDefault_WhenRoleOmitted_IsTrue(string? contributorRoleId)
    {
        Assert.True(ContributorInviteDefaultAccess.IsFullEditByDefault(contributorRoleId));
    }

    [Fact]
    public void IsFullEditByDefault_WhenRoleIsUser_IsTrue()
    {
        Assert.True(ContributorInviteDefaultAccess.IsFullEditByDefault(
            ContributorInviteDefaultAccess.UserRoleId.ToString()));
    }

    [Fact]
    public void IsFullEditByDefault_WhenRoleIsCustom_IsFalse()
    {
        Assert.False(ContributorInviteDefaultAccess.IsFullEditByDefault(Guid.NewGuid().ToString()));
    }
}
