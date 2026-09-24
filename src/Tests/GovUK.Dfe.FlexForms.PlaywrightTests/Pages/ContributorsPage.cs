using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages;

public sealed class ContributorsPage(IPage page, Terminology terminology) : BasePage(page, terminology)
{
    public async Task AddContributorAsync() => await AddContributorButton().ClickAsync();

    public async Task ProceedToFormAsync() => await ProceedToFormButton().ClickAsync();

    public async Task ExpectContributorAsync(int index, string name, string email)
    {
        var row = ContributorRow(index);
        await Assertions.Expect(row).ToContainTextAsync(name);
        await Assertions.Expect(row).ToContainTextAsync(email);
    }

    private ILocator AddContributorButton() => Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add a contributor" });

    private ILocator ProceedToFormButton() => ById("proceed-to-application-form");

    private ILocator ContributorRow(int index) => ById($"contributor-{index}");
}
