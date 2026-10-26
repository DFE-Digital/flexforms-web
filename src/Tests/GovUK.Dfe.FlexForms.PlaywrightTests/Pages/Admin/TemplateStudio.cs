using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;

public sealed class TemplateStudio(IPage page) : BasePage(page)
{
    public async Task OpenFromTemplateManagerAsync(string templateName)
    {
        await Page.GotoAsync("/admin/template-manager");
        var option = Page.Locator("#SelectedTemplateId option").Filter(new LocatorFilterOptions { HasText = templateName + " (" });
        var value = await option.First.GetAttributeAsync("value");
        await ById("SelectedTemplateId").SelectOptionAsync(value!);
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Open template" }).ClickAsync();

        await ById("open-template-studio-button").ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin/template-studio"));
        await Assertions.Expect(Page.Locator(".govuk-caption-l").First).ToHaveTextAsync("Template Studio");
        await Assertions.Expect(ById("tpl-tree").Locator("details").First).ToBeVisibleAsync();
    }

    public async Task CheckForProblemsAsync()
    {
        await ById("tpl-check-button").ClickAsync();
        await Assertions.Expect(ById("tpl-problems").GetByRole(AriaRole.Alert)).ToBeVisibleAsync();
    }

    public async Task<IPage> OpenPreviewAsync()
    {
        var preview = await Page.Context.RunAndWaitForPageAsync(() => ById("tpl-preview-button").ClickAsync());
        await preview.WaitForLoadStateAsync();
        await Assertions.Expect(preview.GetByRole(AriaRole.Region, new PageGetByRoleOptions { Name = "Draft preview" })).ToBeVisibleAsync();
        return preview;
    }

    public async Task StartRuleAsync()
    {
        await Page.GetByRole(AriaRole.Tab, new PageGetByRoleOptions { Name = "Rules" }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add a rule" }).ClickAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Add a rule" })).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Group, new PageGetByRoleOptions { Name = "What should the rule do?" })).ToBeVisibleAsync();
    }

    public async Task AddConditionAsync()
    {
        await ById("tpl-rule-add-condition").ClickAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Condition 2" })).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Group, new PageGetByRoleOptions { Name = "Which conditions must be met?" })).ToBeVisibleAsync();
    }
}
