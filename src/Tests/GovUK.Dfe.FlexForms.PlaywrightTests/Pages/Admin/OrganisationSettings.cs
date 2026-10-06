using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;

public sealed class OrganisationSettings(IPage page, Terminology terminology) : BasePage(page, terminology)
{
    public async Task SetApplicationTerminologyAsync(string singular, string plural)
    {
        await ById("TerminologySingular").FillAsync(singular);
        await ById("TerminologyPlural").FillAsync(plural);
    }

    public async Task SaveSettingsAsync()
    {
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save settings" }).ClickAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("Organisation settings saved.");
    }
}