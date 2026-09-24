using GovUK.Dfe.FlexForms.Web.Extensions;
using Microsoft.AspNetCore.Mvc.Rendering;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Extensions;

public class ConfirmationExtensionsTests
{
    private readonly IHtmlHelper _html = Substitute.For<IHtmlHelper>();

    [Fact]
    public void RenderConfirmationButton_ShouldRenderPlainButton_WhenConfirmationIsNotRequired()
    {
        var html = _html.RenderConfirmationButton(
            "Save",
            handler: "Save",
            buttonId: "save-btn",
            additionalAttributes: new { style = "color:red" }).ToString();

        Assert.Contains("type=\"submit\"", html);
        Assert.Contains("name=\"handler\" value=\"Save\"", html);
        Assert.Contains("id=\"save-btn\"", html);
        Assert.Contains("style=\"color:red\"", html);
        Assert.Contains(">Save</button>", html);
        Assert.DoesNotContain("confirmation-check-", html);
    }

    [Fact]
    public void RenderConfirmationButton_ShouldIncludeHiddenFields_WhenConfirmationIsRequired()
    {
        var html = _html.RenderConfirmationButton(
            "Delete",
            handler: "Delete",
            requiresConfirmation: true,
            displayFields: "name,email",
            title: "Confirm delete",
            requiredMessage: "Tick the box").ToString();

        Assert.Contains("confirmation-check-Delete", html);
        Assert.Contains("confirmation-display-fields-Delete", html);
        Assert.Contains("name,email", html);
        Assert.Contains("Confirm delete", html);
        Assert.Contains("Tick the box", html);
    }

    [Fact]
    public void RenderConfirmationButton_ShouldIncludeDisplayExpression_WhenProvided()
    {
        var html = _html.RenderConfirmationButton(
            "Search",
            handler: "Page",
            requiresConfirmation: true,
            displayExpression: "**displayName**\"\\n**Constituency:** \"constituencyName",
            title: "Is this the right Member of Parliament?").ToString();

        Assert.Contains("confirmation-display-expression-Page", html);
        Assert.Contains("**displayName**", html);
        Assert.Contains("Is this the right Member of Parliament?", html);
    }

    [Fact]
    public void NamedConfirmationButtons_ShouldPassDisplayExpressionThrough()
    {
        const string expression = "name + \"\\n\" + ukprn";

        Assert.Contains(
            "confirmation-display-expression-Page",
            _html.RenderPrimaryConfirmationButton("Primary", displayExpression: expression).ToString());
        Assert.Contains(
            "confirmation-display-expression-Page",
            _html.RenderSecondaryConfirmationButton("Secondary", displayExpression: expression).ToString());
        Assert.Contains(
            "confirmation-display-expression-Page",
            _html.RenderWarningConfirmationButton("Warning", displayExpression: expression).ToString());
        Assert.Contains(
            "confirmation-display-expression-Page",
            _html.RenderLinkConfirmationButton("Link", displayExpression: expression).ToString());
    }

    [Fact]
    public void NamedConfirmationButtons_ShouldUseExpectedCssClasses()
    {
        Assert.Contains("govuk-button", _html.RenderPrimaryConfirmationButton("Primary").ToString());
        Assert.Contains("govuk-button--secondary", _html.RenderSecondaryConfirmationButton("Secondary").ToString());
        Assert.Contains("govuk-button--warning", _html.RenderWarningConfirmationButton("Warning").ToString());
        Assert.Contains("govuk-link", _html.RenderLinkConfirmationButton("Link").ToString());
    }
}
