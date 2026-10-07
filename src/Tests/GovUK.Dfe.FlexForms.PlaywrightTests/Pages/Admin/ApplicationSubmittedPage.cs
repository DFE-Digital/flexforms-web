using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;

public sealed class ApplicationSubmittedPage(IPage page, Terminology terminology) : BasePage(page, terminology)
{
    public async Task GoToAsync(string applicationReference) =>
        await Page.GotoAsync($"/application-submitted/{applicationReference}");

    public async Task ExpectContentAsync(string title, string heading, string body)
    {
        await ExpectHeading(title);
        await ExpectHeading(heading);
        await ExpectText(body);
    }
}