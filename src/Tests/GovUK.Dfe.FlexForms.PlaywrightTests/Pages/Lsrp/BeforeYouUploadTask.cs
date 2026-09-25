using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Lsrp;

public sealed class BeforeYouUploadTask(IPage page) : TaskPage(page)
{
    protected override string TaskItem => "group-financial-summary-task-before-you-upload";

    public async Task CompleteAsync(string localAuthority)
    {
        await SearchAutocompleteAsync("Data_localAuthoritySearch-field-flow-complex-field", localAuthority);
        await ConfirmYesAndContinueAsync();

        await MarkCompleteAndSaveAsync();
    }
}
