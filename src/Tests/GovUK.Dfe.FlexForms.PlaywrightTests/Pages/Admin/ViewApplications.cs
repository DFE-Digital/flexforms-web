using System.Text.RegularExpressions;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Components;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;

public sealed class ViewApplications : BasePage
{
    public ViewApplications(IPage page)
        : base(page)
    {
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
        var deleteButton = ApplicationCard(reference)
            .GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete" });

        await AcceptDialogAsync(() => deleteButton.ClickAsync());
        await Assertions.Expect(Page).ToHaveURLAsync(
            new Regex(@"/admin/applications\?.*CurrentPage=1"));
    }

    public async Task ExpectApplicationListedAsync(string reference, string applicationId, string openHref)
    {
        var card = ApplicationCard(reference);
        await Assertions.Expect(card.GetByRole(AriaRole.Heading, new LocatorGetByRoleOptions { Name = reference, Exact = true }))
            .ToBeVisibleAsync();
        await Assertions.Expect(card).ToContainTextAsync(applicationId);
        await Assertions.Expect(card).ToContainTextAsync(ApplicationsTableFormatting.FormatApplicationDisplayDate());
        await Assertions.Expect(card.GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Open" }))
            .ToHaveAttributeAsync("href", openHref);
        await Assertions.Expect(card.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete" }))
            .ToBeVisibleAsync();
    }

    public async Task ExpectApplicationDeletedAsync(string reference)
    {
        await Assertions.Expect(ApplicationCard(reference).Locator(".govuk-tag", new LocatorLocatorOptions { HasText = "Deleted" }))
            .ToBeVisibleAsync();
    }

    private ILocator ApplicationCard(string reference) =>
        Page.Locator(".admin-application-card")
            .Filter(new LocatorFilterOptions
            {
                Has = Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = reference, Exact = true }),
            });
}