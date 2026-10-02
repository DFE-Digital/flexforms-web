using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages;

public sealed class AuthenticationErrorPage(IPage page, Terminology terminology) : BasePage(page, terminology)
{
    public async Task ExpectInvalidOrExpiredTokensAsync()
    {
        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "There is a problem" }))
            .ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByText("Authentication tokens are invalid or expired")).ToBeVisibleAsync();
    }
}
