using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages;

public abstract class TaskPage(IPage page) : FormPage(page)
{
    protected abstract string TaskItem { get; }

    /// <summary>The task's ID in the template. Only needed by page objects whose tests open pages directly.</summary>
    protected virtual string? TaskId => null;

    public async Task OpenAsync() => await TaskLink().ClickAsync();

    public async Task GoToAsync(string applicationReference, string? pageId = null) =>
        await Page.GotoAsync(pageId is null
            ? $"/applications/{applicationReference}/{RequiredTaskId}"
            : $"/applications/{applicationReference}/{RequiredTaskId}/{pageId}");

    public async Task ExpectSummaryAsync() => await ExpectUrlEndsWithAsync($"/{RequiredTaskId}");

    public async Task ExpectPageAsync(string pageId) => await ExpectUrlEndsWithAsync($"/{RequiredTaskId}/{pageId}");

    public async Task ChangeAnswerAsync(string fieldId) => await ChangeLink(fieldId).ClickAsync();

    public async Task ExpectListedAsync(string fieldId) => await Assertions.Expect(SummaryRow(fieldId)).ToBeVisibleAsync();

    public async Task ExpectNotListedAsync(string fieldId) => await Assertions.Expect(SummaryRow(fieldId)).ToHaveCountAsync(0);

    public async Task UnableToOpenAsync()
    {
        await Assertions.Expect(TaskItemLocator()).ToBeVisibleAsync();
        await Assertions.Expect(TaskName()).ToBeVisibleAsync();
        await Assertions.Expect(TaskLinks()).ToHaveCountAsync(0);
    }

    public async Task ExpectCompletedAsync() => await Assertions.Expect(TaskStatus()).ToContainTextAsync("Completed");

    private string RequiredTaskId =>
        TaskId ?? throw new InvalidOperationException($"{GetType().Name} does not set {nameof(TaskId)}.");

    private ILocator SummaryRow(string fieldId) => ById($"field-{fieldId.ToLowerInvariant()}");

    private ILocator ChangeLink(string fieldId) => ById($"field-{fieldId.ToLowerInvariant()}-change-link");

    private ILocator TaskItemLocator() => ById(TaskItem);

    private ILocator TaskLinks() => TaskItemLocator().GetByRole(AriaRole.Link);

    private ILocator TaskLink() => TaskLinks().First;

    private ILocator TaskName() => TaskItemLocator().Locator(".govuk-task-list__link");

    private ILocator TaskStatus() => TaskItemLocator().Locator(".govuk-task-list__status");
}
