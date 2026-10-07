using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages;

public sealed class ErrorPage(IPage page, Terminology terminology) : BasePage(page, terminology)
{
    public async Task GoToAsync() => await Page.GotoAsync("/Error/NotFound");

    public async Task ExpectInvalidOrExpiredTokensAsync()
    {
        await Assertions
            .Expect(Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "There is a problem" }))
            .ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByText("Authentication tokens are invalid or expired")).ToBeVisibleAsync();
    }

    public async Task ExpectPageNotFoundAsync()
    {
        await Assertions.Expect(Page).ToHaveURLAsync("/Error/NotFound");
        await ExpectHeading("Page not found");
    }
}