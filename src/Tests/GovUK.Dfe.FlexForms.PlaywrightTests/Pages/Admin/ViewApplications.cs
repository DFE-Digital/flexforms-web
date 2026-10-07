using System.Text.RegularExpressions;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Components;
using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;

public sealed class ViewApplications : BasePage
{
    public ApplicationsTable ApplicationsTable { get; }

    public ViewApplications(IPage page, Terminology terminology)
        : base(page, terminology)
    {
        ApplicationsTable = new ApplicationsTable(page);
    }

    public async Task GoToAsync() => await Page.GotoAsync("/admin/applications");

    public async Task SelectATemplateAsync(string templateName)
    {
        await ById("template-selector").SelectOptionAsync(new SelectOptionValue { Label = templateName });
        await Assertions.Expect(Page).ToHaveURLAsync(
            new Regex(@"/admin/applications\?selectedTemplateId=.+"));
    }

    public async Task DeleteApplicationAsync(string reference)
    {
        var deleteButton = ApplicationRow(reference)
            .GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete" });

        await AcceptDialogAsync(() => deleteButton.ClickAsync());
        await Assertions.Expect(Page).ToHaveURLAsync(
            new Regex(@"/admin/applications\?.*CurrentPage=1"));
    }

    private ILocator ApplicationRow(string reference) =>
        Page.Locator("tbody.govuk-table__body > tr.govuk-table__row")
            .Filter(new LocatorFilterOptions
            {
                Has = Page.GetByRole(AriaRole.Cell, new PageGetByRoleOptions { Name = reference, Exact = true }),
            });
}