using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;

public sealed class TenantSettings(IPage page) : BasePage(page)
{
    public async Task GoToAsync() => await Page.GotoAsync("/admin/tenant-settings");

    public async Task RefreshSettingsAsync()
    {
        await GoToAsync();
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Refresh settings" }).ClickAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Alert))
            .ToContainTextAsync("Tenant configuration cache refreshed.");
    }
}