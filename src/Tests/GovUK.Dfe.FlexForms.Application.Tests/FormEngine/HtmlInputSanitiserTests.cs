using GovUK.Dfe.FlexForms.Application.FormEngine;

namespace GovUK.Dfe.FlexForms.Application.Tests.FormEngine;

public class HtmlInputSanitiserTests
{
    [Fact]
    public void Sanitise_normalises_newlines_to_br_tags()
    {
        var result = HtmlInputSanitiser.Sanitise("Some\r\nnew\rlines\nhere");
        Assert.Equal("Some<br>new<br>lines<br>here", result);
    }

    [Fact]
    public void Sanitise_escapes_html_characters()
    {
        var result = HtmlInputSanitiser.Sanitise("<script>alert('hello')</script>");
        Assert.Equal("&lt;script&gt;alert(&#x27;hello&#x27;)&lt;/script&gt;", result);
    }

    [Fact]
    public void Sanitise_escapes_characters_outside_the_latin_set()
    {
        var result = HtmlInputSanitiser.Sanitise("👍");
        Assert.Equal("&#x1F44D;", result);
    }
}
