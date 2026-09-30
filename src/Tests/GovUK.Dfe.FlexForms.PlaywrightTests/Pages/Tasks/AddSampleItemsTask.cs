using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Tasks;

public sealed class AddSampleItemsTask(IPage page) : TaskPage(page)
{
    public const string DescriptionPage = "sample-item-description-page";
    public const string DetailsPage = "sample-item-details-page";

    protected override string TaskItem => "group-collection-flows-task-add-sample-items";

    protected override string TaskId => "sample-items";

    public async Task CompleteAsync(string establishment)
    {
        await StartNewItemAsync();
        await AnswerItemStatusAsync("New");
        await ByIdData("itemDescription").FillAsync("A new sample item");
        await SaveAndContinueAsync();

        await ByIdData("itemName").FillAsync("Sample Item 1");

        await ByIdData("itemCategory").SelectOptionAsync("Category one");
        await SearchAutocompleteAsync("Data_itemEstablishmentSearch-complex-field", establishment);
        await ConfirmYesAndContinueAsync();

        await MarkCompleteAndSaveAsync();
    }

    public async Task StartNewItemAsync() => await ById("sample-items-flow-add-item").ClickAsync();

    public async Task AnswerItemStatusAsync(string status)
    {
        await ChooseAsync(status);
        await SaveAndContinueAsync();
    }

    public async Task ExpectItemPageAsync(string pageId) => await ExpectUrlEndsWithAsync($"/{pageId}");
}
