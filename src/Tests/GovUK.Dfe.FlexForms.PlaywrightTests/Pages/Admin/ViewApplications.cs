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
        var deleteButton = ApplicationCard(reference)
            .GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete" })
            .First;

        await AcceptDialogAsync(() => deleteButton.ClickAsync());
        await Assertions.Expect(Page).ToHaveURLAsync(
            new Regex(@"/admin/applications\?.*CurrentPage=1"));
    }

    public async Task ChangeLeadApplicantAsync(string reference, string email)
    {
        var card = ApplicationCard(reference);

        await card.Locator(".govuk-details__summary").ClickAsync();

        var form = card.Locator(".govuk-details__text form");
        await form.GetByLabel("New lead applicant email").FillAsync(email);
        await form.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Change lead applicant" })
            .ClickAsync();
    }

    public async Task ExpectSuccessAsync(string message) =>
        await Assertions.Expect(Page.Locator(".govuk-notification-banner--success")
                .Filter(new LocatorFilterOptions { Has = ById("owner-change-success-title") }))
            .ToContainTextAsync(message);

    public async Task ExpectErrorAsync(string message) =>
        await Assertions.Expect(Page.Locator(".govuk-error-summary")).ToContainTextAsync(message);

    private ILocator ApplicationCard(string reference) =>
        Page.Locator(".admin-application-card")
            .Filter(new LocatorFilterOptions
            {
                Has = Page.Locator(".govuk-summary-card__title")
                    .GetByText(reference, new LocatorGetByTextOptions { Exact = true }),
            });
}
