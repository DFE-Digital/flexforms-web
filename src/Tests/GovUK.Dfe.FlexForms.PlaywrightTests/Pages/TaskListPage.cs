using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages;

public sealed class TaskListPage(IPage page) : BasePage(page)
{
    public async Task GoToAsync(string applicationReference) =>
        await Page.GotoAsync($"/applications/{applicationReference}");

    public async Task ExpectLoadedAsync() => await Assertions.Expect(Page).ToHaveURLAsync(new Regex(@"/applications/[^/]+$"));

    public async Task InviteContributorsAsync() => await InviteContributorsButton().ClickAsync();

    public async Task ReviewApplicationAsync() => await ReviewApplicationButton().ClickAsync();

    private ILocator InviteContributorsButton() => Page.GetByRole(AriaRole.Button,
        new PageGetByRoleOptions { Name = "Invite contributors" });

    private ILocator ReviewApplicationButton() => ById("review-application-button");
}
