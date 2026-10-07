using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages;

public sealed class AdminPage(IPage page) : BasePage(page)
{
    public async Task GoToAsync() => await Page.GotoAsync("/admin");

    public async Task MakeTemplateLiveAsync(string templateName) =>
        await ClickTemplateActionAsync(templateName, "Make live");

    public async Task MakeTemplateNotLiveAsync(string templateName) =>
        await ClickTemplateActionAsync(templateName, "Make not live");

    public async Task ExpectTemplateLiveAsync(string templateName) =>
        await ExpectTemplateStatusAsync(templateName, "Live");

    public async Task ExpectTemplateNotLiveAsync(string templateName) =>
        await ExpectTemplateStatusAsync(templateName, "Not live");

    private async Task ClickTemplateActionAsync(string templateName, string action)
    {
        var row = TemplateRow(templateName);
        await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions
        {
            Name = action,
            Exact = true,
        }).ClickAsync();
    }

    private async Task ExpectTemplateStatusAsync(string templateName, string expectedStatus)
    {
        var statusCell = await TemplateCellAsync(templateName, "Status");
        await Assertions.Expect(statusCell).ToHaveTextAsync(expectedStatus);
    }

    private ILocator TemplateRow(string templateName) =>
        TemplateTable().GetByRole(AriaRole.Row).Filter(new LocatorFilterOptions
        {
            Has = Page.GetByRole(AriaRole.Cell, new PageGetByRoleOptions { Name = templateName }),
        });

    private ILocator TemplateTable() =>
        Page.GetByRole(AriaRole.Table).Filter(new LocatorFilterOptions
        {
            Has = Page.GetByRole(AriaRole.Columnheader, new PageGetByRoleOptions
            {
                Name = "Status",
                Exact = true,
            }),
        });

    private async Task<ILocator> TemplateCellAsync(string templateName, string columnName)
    {
        var headers = TemplateTable().GetByRole(AriaRole.Columnheader);
        await Assertions.Expect(headers.Filter(new LocatorFilterOptions { HasText = columnName })).ToHaveCountAsync(1);

        var columnIndex = await headers.EvaluateAllAsync<int>(
            "(headers, column) => headers.findIndex((header) => header.textContent?.trim() === column)",
            columnName);

        if (columnIndex < 0)
        {
            throw new InvalidOperationException($"Template table column \"{columnName}\" was not found.");
        }

        return TemplateRow(templateName).GetByRole(AriaRole.Cell).Nth(columnIndex);
    }
    
    public async Task OpenCustomStatusLabelsAsync()
    {
        await ById("go-to-custom-status-button").ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync("/admin/custom-status-label-overrides");
    }

    public async Task OpenUserManagerAsync()
    {
        await ById("go-to-user-manager-button").ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync("/admin/user-manager");
    }

    public async Task OpenContributorManagementAsync()
    {
        await ById("go-to-contributor-management-button").ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync("/admin/contributor-management");
    }

    public async Task OpenRoleManagerAsync()
    {
        await ById("go-to-role-manager-button").ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync("/admin/role-manager");
    }

    public async Task OpenViewApplicationsAsync()
    {
        await ById("go-to-admin-applications-button").ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync("/admin/applications");
    }

    public async Task OpenOrganisationSettingsAsync()
    {
        await ById("go-to-organisation-settings-button").ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync("/admin/organisation-settings");
    }
}