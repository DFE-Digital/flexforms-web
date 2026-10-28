using GovUK.Dfe.FlexForms.Web.Utilities;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Utilities;

public class StoredAnswerHtmlTests
{
    [Fact]
    public void ToSafeLineBreaks_EncodesRawScriptTag()
    {
        var html = StoredAnswerHtml.ToSafeLineBreaks("<script>alert(1)</script>");

        Assert.Equal("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.DoesNotContain("<script>", html);
    }

    [Fact]
    public void ToSafeLineBreaks_DoesNotDoubleEncodeWebPosts()
    {
        var html = StoredAnswerHtml.ToSafeLineBreaks("&lt;b&gt;hi&lt;/b&gt;");

        Assert.Equal("&lt;b&gt;hi&lt;/b&gt;", html);
    }

    [Fact]
    public void ToSafeLineBreaks_InsertsBreaksAfterEncoding()
    {
        var html = StoredAnswerHtml.ToSafeLineBreaks("Line1\n<script>");

        Assert.Equal("Line1<br/>&lt;script&gt;", html);
        Assert.DoesNotContain("<script>", html);
    }
}
