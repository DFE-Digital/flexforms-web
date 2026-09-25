using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Visits;

public sealed class ConversationDetailsTask(IPage page) : TaskPage(page)
{
    protected override string TaskItem => "group-engagement-details-task-conversation-details";

    public async Task CompleteAsync()
    {
        await ById("field-visit-purpose-field-change-link").ClickAsync();
        await ByIdData("visit-purpose-field").FillAsync("Test purpose of the visit");
        await SaveAndContinueAsync();

        await ById("field-visit-notes-field-change-link").ClickAsync();
        await ByIdData("visit-notes-field").FillAsync("Test notes of the visit");
        await SaveAndContinueAsync();

        await MarkCompleteAndSaveAsync();
    }
}
