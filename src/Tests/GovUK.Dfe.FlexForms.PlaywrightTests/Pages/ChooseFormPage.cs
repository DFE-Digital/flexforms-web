using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages;

public sealed class ChooseFormPage(IPage page) : BasePage(page)
{
    public async Task GoToAsync() => await Page.GotoAsync("/templates");

    public async Task SelectFormAsync(string name, string liveStatus)
    {
        var formName = $"{name.Trim()} ({liveStatus.Trim()})";

        await Page
            .GetByRole(AriaRole.Radio, new PageGetByRoleOptions { Name = formName })
            .CheckAsync();
    }

    public async Task GoToDashboardAsync()
    {
        await Page
            .GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Go to dashboard" })
            .ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync("/applications/dashboard");
    }
}