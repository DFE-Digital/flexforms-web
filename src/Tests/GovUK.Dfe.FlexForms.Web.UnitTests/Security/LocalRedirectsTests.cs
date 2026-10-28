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

    [Fact]
    public void OrFallback_KeepsLocalUrl()
        => Assert.Equal("/applications/REF-1", LocalRedirects.OrFallback("/applications/REF-1"));

    [Fact]
    public void OrFallback_UsesCustomFallback_WhenUrlIsNotLocal()
        => Assert.Equal("/safe", LocalRedirects.OrFallback("//evil.example", "/safe"));

    [Theory]
    [InlineData("/path\\to")]
    [InlineData("/\\evil.example")]
    public void IsLocal_RejectsBackslashInPath(string url)
        => Assert.False(LocalRedirects.IsLocal(url));
}
