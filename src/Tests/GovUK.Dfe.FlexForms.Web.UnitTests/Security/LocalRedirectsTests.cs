using GovUK.Dfe.FlexForms.Web.Security;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Security;

public class LocalRedirectsTests
{
    [Theory]
    [InlineData("/applications/dashboard")]
    [InlineData("/")]
    [InlineData("~/applications/dashboard")]
    public void IsLocal_AcceptsRootedPaths(string url)
        => Assert.True(LocalRedirects.IsLocal(url));

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("http://evil.example")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    [InlineData("")]
    [InlineData(null)]
    public void IsLocal_RejectsAbsoluteAndProtocolRelative(string? url)
        => Assert.False(LocalRedirects.IsLocal(url));

    [Fact]
    public void OrFallback_UsesFallback_WhenUrlIsNotLocal()
        => Assert.Equal(LocalRedirects.FallbackPath, LocalRedirects.OrFallback("https://evil.example"));
}
