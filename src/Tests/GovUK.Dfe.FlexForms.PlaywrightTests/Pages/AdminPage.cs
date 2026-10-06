using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages;

public sealed class AdminPage(IPage page, Terminology terminology) : BasePage(page, terminology)
{
    public async Task MakeTemplateLiveAsync(string templateName) =>
        await ClickTemplateActionAsync(templateName, "MakeLive");

    public async Task MakeTemplateNotLiveAsync(string templateName) =>
        await ClickTemplateActionAsync(templateName, "MakeNotLive");

    public async Task ExpectTemplateLiveAsync(string templateName) =>
        await ExpectTemplateStatusAsync(templateName, "Live");

    public async Task ExpectTemplateNotLiveAsync(string templateName) =>
        await ExpectTemplateStatusAsync(templateName, "Not live");

    private async Task ClickTemplateActionAsync(string templateName, string action)
    {
        var row = TemplateRow(templateName);
        await row.Locator($"form[action*='{action}'] button[type='submit']").ClickAsync();
    }

    private async Task ExpectTemplateStatusAsync(string templateName, string expectedStatus)
    {
        var statusTag = TemplateRow(templateName).Locator("td.govuk-table__cell").Nth(1).Locator("strong.govuk-tag");
        await Assertions.Expect(statusTag).ToContainTextAsync(expectedStatus);
    }

    private ILocator TemplateRow(string templateName) =>
        Page.Locator("tr.govuk-table__row").Filter(new LocatorFilterOptions { HasText = templateName }).First;

    public async Task OpenUserManagerAsync()
    {
        await ById("go-to-user-manager-button").ClickAsync();
        await Page.WaitForURLAsync("/admin/user-manager");
    }

    public async Task OpenContributorManagementAsync()
    {
        await ById("go-to-contributor-management-button").ClickAsync();
        await Page.WaitForURLAsync("/admin/contributor-management");
    }

    public async Task OpenRoleManagerAsync()
    {
        await ById("go-to-role-manager-button").ClickAsync();
        await Page.WaitForURLAsync("/admin/role-manager");
    }

    public async Task OpenOrganisationSettingsAsync()
    {
        await ById("go-to-organisation-settings-button").ClickAsync();
        await Page.WaitForURLAsync("/admin/organisation-settings");
    }
}