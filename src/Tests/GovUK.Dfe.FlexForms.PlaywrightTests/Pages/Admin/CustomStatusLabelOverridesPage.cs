using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;

public sealed class CustomStatusLabelOverridesPage(IPage page) : BasePage(page)
{
    public async Task GoToAsync(string templateId) =>
        await Page.GotoAsync($"/admin/custom-status-label-overrides?selectedTemplateId={templateId}");


    public async Task SelectTemplateAsync(string templateId) =>
        await ById("template-selector").SelectOptionAsync(templateId);

    public async Task SaveCustomStatusLabelAsync(ApplicationStatus status, string label)
    {
        await ById("base-status").SelectOptionAsync(status.ToString());
        await ById("BaseStatusOverrideValue").FillAsync(label);
        await ById("save-new-version-button").ClickAsync();
        await Assertions.Expect(Page.GetByText("The custom application statuses have been updated successfully"))
            .ToBeVisibleAsync();
    }
}