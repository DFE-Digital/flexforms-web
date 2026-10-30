using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Components;

public sealed class SummaryCards
{
    private readonly IPage _page;
    private readonly List<Func<Task>> _assertions = [];
    private string _reference = string.Empty;

    public SummaryCards(IPage page)
    {
        _page = page;
    }

    public SummaryCards WithReference(string reference)
    {
        _reference = reference;
        return this;
    }

    public SummaryCards HasTitle(string title)
    {
        Enqueue(async () =>
        {
            var card = Card(title);
            await Assertions.Expect(card).ToBeVisibleAsync();
            await Assertions.Expect(card.Locator(".govuk-summary-card__title")).ToHaveTextAsync(title);
        });
        return this;
    }

    public SummaryCards HasSummaryValue(string key, string expectedValue)
    {
        Enqueue(async () =>
        {
            var card = Card(_reference);
            var summaryRow = card.Locator(".govuk-summary-list__row")
                .Filter(new LocatorFilterOptions { HasText = key });

            await Assertions.Expect(summaryRow).ToHaveCountAsync(1);
            await Assertions.Expect(summaryRow.Locator(".govuk-summary-list__value")).ToContainTextAsync(expectedValue);
        });
        return this;
    }

    public SummaryCards HasLink(string linkText, string href)
    {
        Enqueue(async () =>
        {
            var card = Card(_reference);
            await Assertions.Expect(card.GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = linkText }).First)
                .ToHaveAttributeAsync("href", href);
        });
        return this;
    }

    public SummaryCards HasButton(string buttonText)
    {
        Enqueue(async () =>
        {
            var card = Card(_reference);
            await Assertions.Expect(card.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = buttonText }).First)
                .ToBeVisibleAsync();
        });
        return this;
    }

    public SummaryCards HasText(string expectedText)
    {
        Enqueue(async () =>
        {
            var card = Card(_reference);
            await Assertions.Expect(card).ToContainTextAsync(expectedText);
        });
        return this;
    }

    public async Task VerifyAsync()
    {
        foreach (var assertion in _assertions)
        {
            await assertion();
        }

        _assertions.Clear();
        _reference = string.Empty;
    }

    private void Enqueue(Func<Task> assertion) => _assertions.Add(assertion);

    private ILocator Card(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            throw new InvalidOperationException("Reference/title is not set. Call WithReference() or HasTitle() before asserting summary card content.");
        }

        return _page.Locator(".govuk-summary-card").Filter(new LocatorFilterOptions
        {
            Has = _page.Locator(".govuk-summary-card__title").GetByText(identifier, new LocatorGetByTextOptions { Exact = true }),
        });
    }
}
