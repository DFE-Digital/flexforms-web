using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;

public sealed class CustomStatusLabelOverridesPage(IPage page, Terminology terminology) : BasePage(page, terminology)
{
    public async Task OpenForTemplateAsync(string templateId) =>
        await Page.GotoAsync($"/admin/custom-status-label-overrides?selectedTemplateId={templateId}");

    public async Task OpenFromAdminHubAsync()
    {
        await Page.GotoAsync("/admin");
        await ById("go-to-custom-status-button").ClickAsync();
    }

    public async Task SelectTemplateAsync(string templateId) =>
        await ById("template-selector").SelectOptionAsync(templateId);

    public async Task SaveCustomStatusLabelAsync(ApplicationStatus status, string label)
    {
        await ById("base-status").SelectOptionAsync(status.ToString());
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await ById("BaseStatusOverrideValue").FillAsync(label);
        await ById("save-new-version-button").ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Assertions.Expect(Page.GetByText("The custom application statuses have been updated successfully")).ToBeVisibleAsync();
    }
}
