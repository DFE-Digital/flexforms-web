using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Transfers;

public sealed class ReasonsAndBenefitsOutgoingPage(IPage page) : TaskPage(page)
{
    protected override string TaskItem => "group-about-transferring-academies-task-reason-and-benefits";

    public async Task CompleteAsync()
    {
        await ById("field-reasonandbenefitsacademiesstrategicneeds-change-link").ClickAsync();
        await ById("Data_reasonAndBenefitsAcademiesStrategicNeeds").FillAsync("Strategic needs testing text");
        await SaveAndContinueAsync();

        await ById("field-reasonandbenefitsacademiesmaintainimprove-change-link").ClickAsync();
        await ById("Data_reasonAndBenefitsAcademiesMaintainImprove").FillAsync("Benefits testing text");
        await SaveAndContinueAsync();

        await MarkCompleteAndSaveAsync();
    }
}
