using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Visits;

public sealed class DateOfTheVisitTask(IPage page) : TaskPage(page)
{
    protected override string TaskItem => "group-engagement-details-task-date-of-the-visit";

    public async Task CompleteAsync()
    {
        await ById("field-datevisited-change-link").ClickAsync();
        await EnterDateAsync("Data_dateVisited", "01", "01", "2024");
        await SaveAndContinueAsync();

        await MarkCompleteAndSaveAsync();
    }
}
