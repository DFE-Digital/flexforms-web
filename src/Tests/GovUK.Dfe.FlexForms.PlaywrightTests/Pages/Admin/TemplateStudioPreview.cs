using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;

/// <summary>The Template Studio preview: an unsaved template used as an applicant would, in its own tab.</summary>
public sealed class TemplateStudioPreview(IPage page) : BasePage(page)
{
    public IPage Tab => Page;

    public async Task ExpectOpenAsync() =>
        await Assertions.Expect(Page.GetByRole(AriaRole.Region, new PageGetByRoleOptions { Name = "Draft preview" })).ToBeVisibleAsync();

    public async Task GoToTaskListAsync() =>
        await Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Task list", Exact = true }).ClickAsync();

    public async Task OpenTaskAsync(string taskName) =>
        await Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = taskName, Exact = true }).ClickAsync();

    public async Task StartAsync() =>
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Start", Exact = true }).ClickAsync();

    public async Task ExpectPageAsync(string title) =>
        await Assertions.Expect(ById("page-title")).ToContainTextAsync(title);

    public async Task ChooseAsync(string label) =>
        await Page.GetByRole(AriaRole.Radio, new PageGetByRoleOptions { Name = label, Exact = true }).CheckAsync();

    public async Task FillAsync(string questionId, string answer) => await ById($"Data_{questionId}").FillAsync(answer);

    public async Task SaveAndContinueAsync() => await ById("save-and-continue-button").ClickAsync();

    public async Task GoBackAsync() =>
        await Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Back", Exact = true }).ClickAsync();

    public async Task ExpectErrorAsync(string message) =>
        await Assertions.Expect(Page.Locator(".govuk-error-summary")).ToContainTextAsync(message);

    public async Task ExpectAnswerAsync(string question, string answer) =>
        await Assertions.Expect(SummaryRow(question).Locator(".govuk-summary-list__value")).ToContainTextAsync(answer);

    public async Task ExpectNotListedAsync(string question) =>
        await Assertions.Expect(SummaryRow(question)).ToHaveCountAsync(0);

    public async Task AddItemAsync(string buttonText) =>
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = buttonText, Exact = true }).ClickAsync();

    public async Task ExpectItemAsync(string title) =>
        await Assertions.Expect(ItemCard(title)).ToBeVisibleAsync();

    public async Task ExpectNoItemAsync(string title) =>
        await Assertions.Expect(ItemCard(title)).ToHaveCountAsync(0);

    public async Task RemoveItemAsync(string title) =>
        await ItemCard(title).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = $"Remove {title}" }).ClickAsync();

    public async Task ExpectSuccessAsync(string message) =>
        await Assertions.Expect(Page.Locator(".govuk-notification-banner--success")).ToContainTextAsync(message);

    public async Task MarkCompleteAsync()
    {
        await Page.GetByRole(AriaRole.Radio, new PageGetByRoleOptions { Name = "Yes, I've completed this section" }).CheckAsync();
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Continue", Exact = true }).ClickAsync();
    }

    public async Task ExpectCompletionBlockedByAsync(string message) =>
        await Assertions.Expect(Page.Locator(".govuk-error-summary")).ToContainTextAsync(message);

    public async Task ExpectTaskStatusAsync(string taskName, string status) =>
        await Assertions.Expect(Page.Locator(".govuk-task-list__item").Filter(new LocatorFilterOptions
        {
            Has = Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = taskName, Exact = true })
        }).Locator(".govuk-task-list__status")).ToHaveTextAsync(status);

    private ILocator SummaryRow(string question) =>
        Page.Locator(".govuk-summary-list__row").Filter(new LocatorFilterOptions
        {
            Has = Page.Locator(".govuk-summary-list__key").GetByText(question, new LocatorGetByTextOptions { Exact = true })
        });

    private ILocator ItemCard(string title) =>
        Page.Locator(".govuk-summary-card").Filter(new LocatorFilterOptions
        {
            Has = Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = title, Exact = true })
        });
}
