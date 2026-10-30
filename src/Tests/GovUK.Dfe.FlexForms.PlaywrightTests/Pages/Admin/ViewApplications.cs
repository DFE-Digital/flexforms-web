using System.Text.RegularExpressions;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Components;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;

public sealed class ViewApplications : BasePage
{
    public SummaryCards SummaryCards { get; }

    public ViewApplications(IPage page)
        : base(page)
    {
        SummaryCards = new SummaryCards(page);
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
        var deleteButton = Page.Locator(".admin-application-card")
            .Filter(new LocatorFilterOptions
            {
                Has = Page.Locator(".govuk-summary-card__title").GetByText(reference, new LocatorGetByTextOptions { Exact = true }),
            })
            .GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete" })
            .First;

        await AcceptDialogAsync(() => deleteButton.ClickAsync());
        await Assertions.Expect(Page).ToHaveURLAsync(
            new Regex(@"/admin/applications\?.*CurrentPage=1"));
    }
}