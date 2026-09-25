using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages;

public abstract class TaskPage(IPage page) : FormPage(page)
{
    protected abstract string TaskItem { get; }

    public async Task OpenAsync() => await TaskLink().ClickAsync();

    public async Task UnableToOpenAsync()
    {
        await Assertions.Expect(TaskItemLocator()).ToBeVisibleAsync();
        await Assertions.Expect(TaskName()).ToBeVisibleAsync();
        await Assertions.Expect(TaskLinks()).ToHaveCountAsync(0);
    }

    public async Task ExpectCompletedAsync() => await Assertions.Expect(TaskStatus()).ToContainTextAsync("Completed");

    private ILocator TaskItemLocator() => ById(TaskItem);

    private ILocator TaskLinks() => TaskItemLocator().GetByRole(AriaRole.Link);

    private ILocator TaskLink() => TaskLinks().First;

    private ILocator TaskName() => TaskItemLocator().Locator(".govuk-task-list__link");

    private ILocator TaskStatus() => TaskItemLocator().Locator(".govuk-task-list__status");
}
