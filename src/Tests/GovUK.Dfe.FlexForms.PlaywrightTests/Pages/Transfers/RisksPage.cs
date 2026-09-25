using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Transfers;

public sealed class RisksPage(IPage page) : TaskPage(page)
{
    protected override string TaskItem => "group-about-transferring-academies-task-risks";

    public async Task CompleteAsync()
    {
        // Due diligence
        await ById("field-risksduediligence-change-link").ClickAsync();
        await ById("Data_risksDueDiligence").FillAsync("Due diligence testing text");
        await SaveAndContinueAsync();

        // Pupil numbers -> Yes + upload
        await ById("field-riskspupilnumbers-change-link").ClickAsync();
        await ById("Data_risksPupilNumbers_").CheckAsync();
        await SaveAndContinueAsync();
        await UploadFileAsync("risksUploadPupilNumbers");

        // Type of transfer -> first option, financial deficit -> Yes + forecast upload
        await ById("field-riskstransfertype-change-link").ClickAsync();
        await ById("Data_risksTransferType_").CheckAsync();
        await SaveAndContinueAsync();
        await ById("Data_risksFinancialDeficit_").CheckAsync();
        await SaveAndContinueAsync();
        await UploadFileAsync("risksFinancialForecast");

        // Other risks -> Yes + summary
        await ById("Data_risksOtherRisks_").CheckAsync();
        await SaveAndContinueAsync();
        await ById("Data_risksRiskManagement").FillAsync("Other risks testing text");
        await SaveAndContinueAsync();

        // Finances pooled -> GAG pooled (walks through the retained pages)
        await ById("field-risksfinancespooled-change-link").ClickAsync();
        await ById("Data_risksFinancesPooled_").CheckAsync();
        await SaveAndContinueAsync();
        await SaveAndContinueAsync();
        await SaveAndContinueAsync();

        // Surplus funds / reserves transfer
        await ById("field-risksreservestransfer-change-link").ClickAsync();
        await ById("Data_risksReservesTransfer").FillAsync("Surplus funds testing text");
        await SaveAndContinueAsync();

        await MarkCompleteAndSaveAsync();
    }
}
