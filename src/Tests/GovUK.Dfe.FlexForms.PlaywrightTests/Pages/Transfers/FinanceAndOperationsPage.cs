using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Transfers;

public sealed class FinanceAndOperationsPage(IPage page) : TaskPage(page)
{
    private const string UploadField = "financeAndOperationsUploadGrowthPlanNext3Years";

    protected override string TaskItem => "group-about-the-trust-that-academies-are-joining-task-finance-and-operations";

    public async Task CompleteAsync()
    {
        // Growth plan -> Yes, then upload
        await ById("field-financeandoperationshavegrowthplannext3years-change-link").ClickAsync();
        await ById("Data_financeAndOperationsHaveGrowthPlanNext3Years_").CheckAsync();
        await SaveAndContinueAsync();
        await UploadFileAsync(UploadField);

        // Policy on charges made to academies -> Yes + text
        await ById("field-financeandoperationspolicyonchargesmadetoitsacademies-change-link").ClickAsync();
        await ById("Data_financeAndOperationsPolicyOnChargesMadeToItsAcademies_").CheckAsync();
        await SaveAndContinueAsync();
        await ById("Data_financeAndOperationsHowWillPolicyOnChargesMadeToItsAcademies").FillAsync(
            "Charge on academies testing text");
        await SaveAndContinueAsync();

        // Service level agreements -> Yes / Yes + text
        await ById("field-financeandoperationshavesapacademies-change-link").ClickAsync();
        await ById("Data_financeAndOperationsHaveSAPAcademies_").CheckAsync();
        await SaveAndContinueAsync();
        await ById("Data_financeAndOperationsLocalAuthorityAgreements_").CheckAsync();
        await SaveAndContinueAsync();
        await ById("Data_financeAndOperationsSummariseTheAgreements").FillAsync("Alternative agreements testing text");
        await SaveAndContinueAsync();

        await MarkCompleteAndSaveAsync();
    }
}
