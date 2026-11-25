using System.Text.Encodings.Web;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Web.Services;
using Microsoft.AspNetCore.Html;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Services;

public class QuestionAnchorTests
{
    [Fact]
    public void ElementId_prefixes_the_field_id()
    {
        Assert.Equal("question-fullName", QuestionAnchor.ElementId("fullName"));
    }

    [Fact]
    public void ElementId_replaces_whitespace_so_the_id_is_valid_html()
    {
        Assert.Equal("question-Lead-attendee-name", QuestionAnchor.ElementId("Lead attendee  name"));
    }

    [Fact]
    public void Fragment_is_the_url_escaped_element_id()
    {
        Assert.Equal("#question-fullName", QuestionAnchor.Fragment("fullName"));
        Assert.Equal("#question-a%26b", QuestionAnchor.Fragment("a&b"));
    }

    [Theory]
    [InlineData("text")]
    [InlineData("autocomplete")]
    [InlineData("complexField")]
    public void WrapInQuestionAnchor_renders_the_anchor_around_the_field_content(string fieldType)
    {
        var field = NewField("files", fieldType);

        var html = Render(FieldRendererService.WrapInQuestionAnchor(field, new HtmlString("<input id=\"Data_files\" />")));

        Assert.Equal("<div id=\"question-files\"><input id=\"Data_files\" /></div>", html);
    }

    [Fact]
    public void WrapInQuestionAnchor_still_renders_the_anchor_when_the_field_renders_no_input()
    {
        // e.g. an upload field whose limit is reached: the partial omits the file input and form entirely.
        var field = NewField("files", "complexField");
        var limitReached = new HtmlString("<div class=\"govuk-inset-text\" id=\"upload-limit-reached-files\">Limit reached</div>");

        var html = Render(FieldRendererService.WrapInQuestionAnchor(field, limitReached));

        Assert.Contains("id=\"question-files\"", html);
        Assert.Contains("Limit reached", html);
        Assert.DoesNotContain("Data_files", html);
    }

    [Fact]
    public void WrapInQuestionAnchor_matches_the_fragment_used_by_change_links()
    {
        var field = NewField("Lead attendee", "text");

        var html = Render(FieldRendererService.WrapInQuestionAnchor(field, HtmlString.Empty));

        // A "#<id>" fragment must resolve to the rendered element's id.
        var fragmentId = Uri.UnescapeDataString(QuestionAnchor.Fragment(field.FieldId).TrimStart('#'));
        Assert.Contains($"id=\"{fragmentId}\"", html);
    }

    private static Field NewField(string fieldId, string type) =>
        new() { FieldId = fieldId, Type = type, Label = new() { Value = fieldId }, Order = 1 };

    private static string Render(IHtmlContent content)
    {
        using var writer = new StringWriter();
        content.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }
}

