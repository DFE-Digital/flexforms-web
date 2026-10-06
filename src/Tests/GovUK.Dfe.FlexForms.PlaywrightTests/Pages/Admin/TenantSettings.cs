using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;

public sealed class TenantSettings(IPage page, Terminology terminology) : BasePage(page, terminology)
{
    public async Task RefreshSettingsAsync()
    {
        await Page.GotoAsync("/admin/tenant-settings");
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Refresh settings" }).ClickAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Alert))
            .ToContainTextAsync("Tenant configuration cache refreshed.");
    }
}