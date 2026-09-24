using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Transfers;

public sealed class ReasonsAndBenefitsIncomingPage(IPage page) : TaskPage(page)
{
    protected override string TaskItem => "group-about-the-trust-that-academies-are-joining-task-reason-and-benefits";

    public async Task CompleteAsync()
    {
        await ById("field-reasonandbenefitstruststrategicneeds-change-link").ClickAsync();
        await ById("Data_reasonAndBenefitsTrustStrategicNeeds").FillAsync("Strategic needs testing text");
        await SaveAndContinueAsync();

        await ById("field-reasonandbenefitstrustdevelopmentalneeds-change-link").ClickAsync();
        await ById("Data_reasonAndBenefitsTrustDevelopmentalNeeds").FillAsync("Maintain and improve testing text");
        await SaveAndContinueAsync();

        await ById("field-reasonandbenefitstrustacademiestrustsworkedtogether-change-link").ClickAsync();
        // Have the academies and trusts worked together in the past? -> Yes
        await ById("Data_reasonAndBenefitsTrustAcademiesTrustsWorkedTogether_").CheckAsync();
        await SaveAndContinueAsync();
        await ById("Data_reasonAndBenefitsTrustHowHaveAcademiesTrustsWorkedTogether").FillAsync(
            "Worked together testing text");
        await SaveAndContinueAsync();

        await MarkCompleteAndSaveAsync();
    }
}
