using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages;

public sealed class ApplicationSubmittedPage(IPage page) : BasePage(page)
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