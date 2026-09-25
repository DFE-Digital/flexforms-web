using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Tasks;

public sealed record ConfiguredFieldsData(string Academy, string LocalAuthority, string Diocese, string Mp);

public sealed class ConfiguredFieldsTask(IPage page) : TaskPage(page)
{
    protected override string TaskItem => "group-configured-fields-task-complete-configured-fields";

    public async Task CompleteAsync(ConfiguredFieldsData data)
    {
        await SearchAutocompleteAsync("Data_establishmentSearch-complex-field", data.Academy);
        await ConfirmYesAndContinueAsync();

        await SearchAutocompleteAsync("Data_localAuthoritySearch-complex-field", data.LocalAuthority);
        await ConfirmYesAndContinueAsync();

        await SearchAutocompleteAsync("Data_dioceseSearch-complex-field", data.Diocese);
        await ConfirmYesAndContinueAsync();

        await SearchAutocompleteAsync("Data_mpSearch-complex-field", data.Mp);
        await ConfirmYesAndContinueAsync();

        await UploadFileAsync("supportingDocuments");
        await MarkCompleteAndSaveAsync();
    }
}
