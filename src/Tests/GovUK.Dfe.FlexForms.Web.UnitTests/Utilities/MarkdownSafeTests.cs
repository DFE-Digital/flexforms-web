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

    [Fact]
    public void ToSafeGovUkHtml_ShouldRenderHeadingsListsAndMailto()
    {
        var html = MarkdownSafe.ToSafeGovUkHtml(
            "## What happens next\n\n- if we need anything else\n\nEmail [team@example.com](mailto:team@example.com).");

        Assert.Contains("govuk-heading-m", html);
        Assert.Contains("What happens next", html);
        Assert.Contains("govuk-list govuk-list--bullet", html);
        Assert.Contains("govuk-link", html);
        Assert.Contains("mailto:team@example.com", html);
        Assert.Contains("govuk-body", html);
    }

    [Fact]
    public void ToSafeGovUkHtml_ShouldKeepMailtoInSameTabButOpenHttpsLinksInNewTab()
    {
        var html = MarkdownSafe.ToSafeGovUkHtml(
            "Email [team@example.com](mailto:team@example.com) or read the [guidance](https://example.com).");

        var mailtoAnchor = AnchorContaining(html, "mailto:team@example.com");
        Assert.DoesNotContain("target=", mailtoAnchor);
        Assert.DoesNotContain("rel=", mailtoAnchor);

        var httpsAnchor = AnchorContaining(html, "https://example.com");
        Assert.Contains("target=\"_blank\"", httpsAnchor);
        Assert.Contains("noopener", httpsAnchor);
    }

    private static string AnchorContaining(string html, string href) =>
        html.Split("<a ")
            .Select(segment => segment[..segment.IndexOf('>')])
            .Single(openingTag => openingTag.Contains(href, StringComparison.Ordinal));

    [Fact]
    public void ToSafeGovUkHtml_ShouldStripScripts()
    {
        var html = MarkdownSafe.ToSafeGovUkHtml("Hello <script>alert(1)</script>");
        Assert.DoesNotContain("<script>", html);
    }
}
