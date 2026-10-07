using System.Text.RegularExpressions;
using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;

public sealed class ContributorManagement(IPage page, Terminology terminology) : BasePage(page, terminology)
{
    public CreatedApplications CreatedApplications { get; } = new(page);

    public async Task GoToAsync() => await Page.GotoAsync("/admin/contributor-management");

    public async Task LookupAnApplicationByReferenceNumberAsync(string referenceNumber)
    {
        await ById("ReferenceNumber").FillAsync(referenceNumber);
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Look up contributors" }).ClickAsync();
    }

    public async Task LookupApplicationsByUserAsync(string email)
    {
        await ById("Email").FillAsync(email);
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Look up applications and invites" })
            .ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(
            new Regex(@"/admin/contributor-management\?email=.+"));
    }

    public async Task ExpectHasContributorAsync(string reference, string email)
    {
        await Assertions.Expect(Page.Locator("p.govuk-body").Filter(new LocatorFilterOptions
        {
            HasText = $"Reference {reference}",
        })).ToBeVisibleAsync();

        await Assertions.Expect(ContributorRow(email)).ToBeVisibleAsync();
    }

    private ILocator ContributorRow(string email) =>
        Page.Locator("table.govuk-table tbody.govuk-table__body tr.govuk-table__row")
            .Filter(new LocatorFilterOptions { HasText = email });
}

/// <summary>
/// Fluent checks for applications returned by an email lookup.
/// Chain methods, then await VerifyAsync(). Verification searches later pages when the application is not on the current one.
/// </summary>
public sealed class CreatedApplications(IPage page)
{
    private readonly List<Func<ILocator, Task>> _assertions = [];
    private string _reference = string.Empty;

    public CreatedApplications HasApplication(string reference)
    {
        _reference = reference;
        return this;
    }

    public CreatedApplications WithInvitee(string email)
    {
        Enqueue(card => Assertions.Expect(InviteeRow(card, email)).ToBeVisibleAsync());
        return this;
    }

    public CreatedApplications WithInviteLink()
    {
        Enqueue(async card =>
        {
            var link = card.GetByRole(AriaRole.Link,
                new LocatorGetByRoleOptions { Name = "Open full invite and remove UI" });
            var contributorsPath = $"/applications/{Uri.EscapeDataString(_reference)}/contributors";
            await Assertions.Expect(link).ToBeVisibleAsync();
            await Assertions.Expect(link).ToHaveAttributeAsync("href", new Regex(Regex.Escape(contributorsPath)));
        });
        return this;
    }

    public async Task VerifyAsync()
    {
        if (string.IsNullOrEmpty(_reference))
        {
            throw new InvalidOperationException(
                "Application reference is not set. Call HasApplication() before VerifyAsync().");
        }

        await OpenPageContainingAsync(_reference);

        var card = ApplicationCard(_reference);
        await Assertions.Expect(card).ToBeVisibleAsync();

        foreach (var assertion in _assertions)
        {
            await assertion(card);
        }

        _assertions.Clear();
        _reference = string.Empty;
    }

    private void Enqueue(Func<ILocator, Task> assertion) => _assertions.Add(assertion);

    private async Task OpenPageContainingAsync(string reference)
    {
        await GoToFirstResultsPageAsync();

        for (var pageIndex = 0; pageIndex < 10; pageIndex++)
        {
            if (await ApplicationCard(reference).CountAsync() > 0)
            {
                return;
            }

            var next = page.Locator("nav[aria-label='Pagination'] a[rel='next']");
            if (await next.CountAsync() == 0)
            {
                return;
            }

            var url = page.Url;
            await next.ClickAsync();
            if (page.Url == url)
            {
                return;
            }
        }
    }

    private async Task GoToFirstResultsPageAsync()
    {
        var pageOne = page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Page 1", Exact = true });
        if (await pageOne.CountAsync() == 0 || await pageOne.GetAttributeAsync("aria-current") == "page")
        {
            return;
        }

        await pageOne.ClickAsync();
    }

    private ILocator ApplicationCard(string reference) =>
        page.Locator(".govuk-summary-card").Filter(new LocatorFilterOptions
        {
            Has = page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = reference }),
        });

    private static ILocator InviteeRow(ILocator card, string email) =>
        card.Locator("tbody.govuk-table__body tr.govuk-table__row")
            .Filter(new LocatorFilterOptions { HasText = email });
}