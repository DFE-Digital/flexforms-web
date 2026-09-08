using GovUK.Dfe.FlexForms.Web.Utilities;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Utilities;

public class MarkdownSafeTests
{
    [Fact]
    public void ToSafeHtml_ShouldReturnEmpty_WhenBlank()
    {
        Assert.Equal(string.Empty, MarkdownSafe.ToSafeHtml(" "));
        Assert.Equal(string.Empty, MarkdownSafe.ToSafeHtml(null));
    }

    [Fact]
    public void ToSafeHtml_ShouldRenderMarkdownAndSanitiseHtml()
    {
        var html = MarkdownSafe.ToSafeHtml("**bold** and [link](https://example.com)\n<script>alert(1)</script>");

        Assert.Contains("<strong>bold</strong>", html);
        Assert.Contains("target=\"_blank\"", html);
        Assert.Contains("noopener", html);
        Assert.DoesNotContain("<script>", html);
    }

    [Fact]
    public void ToSafeHtml_ShouldTruncateLongInput()
    {
        var html = MarkdownSafe.ToSafeHtml(new string('a', 50), maxChars: 10);
        Assert.DoesNotContain(new string('a', 50), html);
    }
}
