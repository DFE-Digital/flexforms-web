using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Transfers;

public sealed class DetailsOfAcademiesPage(IPage page) : TaskPage(page)
{
    private const string SearchInput = "Data_academiesSearch-complex-field";

    protected override string TaskItem => "group-about-transferring-academies-task-details-of-academies";

    public async Task CompleteAsync(string academyName)
    {
        await ById("detailsOfAcademies-add-item").ClickAsync();

        await SearchAutocompleteAsync(SearchInput, academyName);
        await ConfirmYesAndContinueAsync();

        await EnterDateAsync("Data_proposedTransferDate", "01", "12", "2024");
        await SaveAndContinueAsync();

        // Does the academy receive additional funding? -> No
        await ById("Data_academyFunding_-2").CheckAsync();
        await SaveAndContinueAsync();
        await ById("Data_academyOperatingDifferently").FillAsync("Academy will operate differently testing text");
        await SaveAndContinueAsync();

        // Diocesan consent required? -> No
        await ById("Data_detailsOfAcademiesDiocesanConsent_-2").CheckAsync();
        await SaveAndContinueAsync();

        await MarkCompleteAndSaveAsync();
    }
}
