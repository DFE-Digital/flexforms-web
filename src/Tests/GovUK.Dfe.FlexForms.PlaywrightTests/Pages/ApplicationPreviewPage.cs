using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages;

public sealed class ApplicationPreviewPage(IPage page) : FormPage(page)
{
    public async Task SubmitAsync() => await SubmitApplicationButton().ClickAsync();

    public async Task SubmitButtonNotVisibleAsync()
    {
        await Assertions.Expect(Preview()).ToBeVisibleAsync();
        await Assertions.Expect(SubmitApplicationButton()).ToHaveCountAsync(0);
    }

    public async Task ExpectNonLeadApplicantCannotSubmitAsync(string singular)
    {
        await SubmitButtonNotVisibleAsync();
        await Assertions.Expect(LeadApplicantSubmitMessage(singular)).ToBeVisibleAsync();
    }

    public async Task ExpectSubmittedAsync(string submitMessage = "submitted")
    {
        await Assertions.Expect(Page).ToHaveURLAsync(new Regex(@"/application-submitted/"));
        await Assertions.Expect(SubmittedHeading(submitMessage)).ToBeVisibleAsync();
    }

    private ILocator Preview() => Page.Locator("[data-application-preview=\"true\"]");

    private ILocator SubmitApplicationButton() => ById("submit-application-button");

    private ILocator LeadApplicantSubmitMessage(string singular) =>
        Page.GetByText($"Only the lead applicant can submit this {singular}.", new PageGetByTextOptions { Exact = true });

    private ILocator SubmittedHeading(string submitMessage) => Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = submitMessage });
}
