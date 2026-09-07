using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Http;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Security;

public class AuthenticationPathExclusionsTests
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/signin-oidc")]
    [InlineData("/Logout")]
    [InlineData("/css/site.css")]
    [InlineData("/images/logo.png")]
    [InlineData("/script.js")]
    public void ShouldSkip_ShouldIgnoreAuthHealthAndStaticPaths(string path)
    {
        Assert.True(AuthenticationPathExclusions.ShouldSkip(new PathString(path)));
    }

    [Fact]
    public void ShouldSkip_ShouldAllowApplicationPaths()
    {
        Assert.False(AuthenticationPathExclusions.ShouldSkip(PathString.Empty));
        Assert.False(AuthenticationPathExclusions.ShouldSkip(new PathString("/applications/REF-1")));
    }
}
