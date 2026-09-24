using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages;

public sealed class ContributorsInvitePage(IPage page, Terminology terminology) : BasePage(page, terminology)
{
    public async Task FillInviteAsync(string name, string email)
    {
        await NameInput().FillAsync(name);
        await EmailAddressInput().FillAsync(email);
    }

    public async Task SendInviteAsync() => await SendInviteButton().ClickAsync();

    private ILocator NameInput() => Page.GetByLabel("Full name");

    private ILocator EmailAddressInput() => Page.GetByLabel("Email address");

    private ILocator SendInviteButton() => Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Send email invite" });
}
