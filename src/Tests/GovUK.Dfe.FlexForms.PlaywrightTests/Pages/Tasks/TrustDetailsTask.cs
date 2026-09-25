using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Tasks;

public sealed class TrustDetailsTask(IPage page) : TaskPage(page)
{
    protected override string TaskItem => "group-collection-flows-task-trust-details";

    public async Task CompleteAsync(string trust)
    {
        await ById("details-of-trust-flow-add-item").ClickAsync();

        await SearchAutocompleteAsync("Data_trustSearch-complex-field", trust);
        await ConfirmYesAndContinueAsync();
        await MarkCompleteAndSaveAsync();
    }
}
