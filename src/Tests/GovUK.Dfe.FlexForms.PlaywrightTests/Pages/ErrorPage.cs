using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages;

public sealed class ErrorPage(IPage page) : BasePage(page)
{
    public async Task ExpectPermissionErrorAsync()
    {
        await Assertions
            .Expect(Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "You do not have permission" }))
            .ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByText("You are not authorised to access this service")).ToBeVisibleAsync();
    }

    public async Task ExpectPageNotFoundAsync()
    {
        await Assertions.Expect(Page).ToHaveURLAsync("/Error/NotFound");
        await ExpectHeading("Page not found");
    }
}