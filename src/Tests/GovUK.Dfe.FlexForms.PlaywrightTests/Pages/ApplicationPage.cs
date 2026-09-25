using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages;

public sealed class ApplicationPage(IPage page, Terminology terminology) : BasePage(page, terminology)
{
    public async Task InviteContributorsAsync() => await InviteContributorsButton().ClickAsync();

    public async Task GoToSectionAsync(string name) => await SectionLink(name).ClickAsync();

    private ILocator InviteContributorsButton() => Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Invite contributors" });

    private ILocator SectionLink(string name) => Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = name });
}
