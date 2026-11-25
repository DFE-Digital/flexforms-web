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
    public void ElementId_encodes_whitespace_so_the_id_is_valid_html()
    {
        var id = QuestionAnchor.ElementId("Lead attendee  name");

        Assert.Equal("question-Lead%20attendee%20%20name", id);
        Assert.DoesNotContain(' ', id);
    }

    [Fact]
    public void Fragment_is_the_hash_prefixed_element_id()
    {
        Assert.Equal("#question-fullName", QuestionAnchor.Fragment("fullName"));
        Assert.Equal("#question-a%26b", QuestionAnchor.Fragment("a&b"));
        Assert.Equal("#question-Lead%20attendee", QuestionAnchor.Fragment("Lead attendee"));
    }

    [Fact]
    public void ElementId_and_Fragment_are_distinct_for_ids_differing_only_by_whitespace_or_hyphen()
    {
        // Regression: both previously normalised to "question-Lead-attendee", so on one page the
        // duplicate wrapper ids made both Change links target the first question.
        Assert.Equal("question-Lead%20attendee", QuestionAnchor.ElementId("Lead attendee"));
        Assert.Equal("question-Lead-attendee", QuestionAnchor.ElementId("Lead-attendee"));
        Assert.NotEqual(QuestionAnchor.ElementId("Lead attendee"), QuestionAnchor.ElementId("Lead-attendee"));
        Assert.NotEqual(QuestionAnchor.Fragment("Lead attendee"), QuestionAnchor.Fragment("Lead-attendee"));
    }

    [Fact]
    public void ElementId_does_not_trim_or_collide_with_literal_percent_sequences()
    {
        Assert.NotEqual(QuestionAnchor.ElementId("a b"), QuestionAnchor.ElementId(" a b"));
        Assert.NotEqual(QuestionAnchor.ElementId("a b"), QuestionAnchor.ElementId("a b "));
        Assert.NotEqual(QuestionAnchor.ElementId("a b"), QuestionAnchor.ElementId("a%20b"));
    }

    [Fact]
    public void WrapInQuestionAnchor_renders_unique_ids_for_a_colliding_pair_on_one_page()
    {
        var html =
            Render(FieldRendererService.WrapInQuestionAnchor(NewField("Lead attendee", "text"), HtmlString.Empty)) +
            Render(FieldRendererService.WrapInQuestionAnchor(NewField("Lead-attendee", "text"), HtmlString.Empty));

        Assert.Equal(
            "<div id=\"question-Lead%20attendee\"></div><div id=\"question-Lead-attendee\"></div>",
            html);
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

        // A "#<id>" fragment must match the rendered element's id exactly (browsers try the raw fragment first).
        var fragmentId = QuestionAnchor.Fragment(field.FieldId).TrimStart('#');
        Assert.Equal(QuestionAnchor.ElementId(field.FieldId), fragmentId);
        Assert.Equal("<div id=\"question-Lead%20attendee\"></div>", html);
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

