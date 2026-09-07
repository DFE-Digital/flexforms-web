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
    public void NamedConfirmationButtons_ShouldUseExpectedCssClasses()
    {
        Assert.Contains("govuk-button", _html.RenderPrimaryConfirmationButton("Primary").ToString());
        Assert.Contains("govuk-button--secondary", _html.RenderSecondaryConfirmationButton("Secondary").ToString());
        Assert.Contains("govuk-button--warning", _html.RenderWarningConfirmationButton("Warning").ToString());
        Assert.Contains("govuk-link", _html.RenderLinkConfirmationButton("Link").ToString());
    }
}
