using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Tasks;

public sealed class AddSampleItemsTask(IPage page) : TaskPage(page)
{
    protected override string TaskItem => "group-collection-flows-task-add-sample-items";

    public async Task CompleteAsync(string establishment)
    {
        await ById("sample-items-flow-add-item").ClickAsync();

        await ByIdData("itemName").FillAsync("Sample Item 1");

        await ByIdData("itemCategory").SelectOptionAsync("Category one");
        await SearchAutocompleteAsync("Data_itemEstablishmentSearch-complex-field", establishment);
        await ConfirmYesAndContinueAsync();

        await MarkCompleteAndSaveAsync();
    }
}
