using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Visits;

public sealed class VisitOrganisationTask(IPage page) : TaskPage(page)
{
    protected override string TaskItem => "group-engagement-details-task-visit-organisation";

    public async Task CompleteAsync() => await MarkCompleteAndSaveAsync();

    public async Task AddTrustAsync(string trustName) => await AddOrganisationWithSearchAsync(trustName, "incomingTrustsSearch-field-flow");

    public async Task AddSchoolAsync(string schoolName) => await AddOrganisationWithSearchAsync(schoolName, "academiesSearch", "-2");

    public async Task AddLocalAuthorityAsync(string laName) => await AddOrganisationWithSearchAsync(laName, "localauthoritySearch-field-flow", "-3");

    public async Task AddDioceseAsync(string dioceseName) => await AddOrganisationWithSearchAsync(dioceseName, "dioceseSearch-field-flow", "-4");

    public async Task AddOtherOrganisationAsync(string orgName) => await AddOrganisationAsync(orgName, "other-type", "-5");

    public async Task AddDfEOrganisedConferenceAsync(string orgName) => await AddOrganisationAsync(orgName, "dfe-conference", "-6");

    public async Task AddExternallyOrganisedConferenceAsync(string orgName) => await AddOrganisationAsync(orgName, "external-conference", "-7");

    private async Task AddOrganisationWithSearchAsync(string orgName, string orgSearchField, string orgNumber = "")
    {
        await ChooseOrganisationTypeAsync(orgNumber);

        await SearchAutocompleteAsync($"Data_{orgSearchField}-complex-field", orgName);
        await ConfirmYesAndContinueAsync();

        await ByIdData("visitLocationSelection_-9").CheckAsync();
        await SaveAndContinueAsync();
    }

    private async Task AddOrganisationAsync(string orgName, string orgField, string orgNumber)
    {
        await ChooseOrganisationTypeAsync(orgNumber);

        await ByIdData($"organisation-{orgField}-field").FillAsync(orgName);
        await SaveAndContinueAsync();

        await ByIdData("visitLocationSelection_-3").CheckAsync();
        await SaveAndContinueAsync();
    }

    private async Task ChooseOrganisationTypeAsync(string orgNumber)
    {
        await ById("visitOrganisations-add-item").ClickAsync();
        await ByIdData($"organisationTypeSelection_{orgNumber}").CheckAsync();
        await SaveAndContinueAsync();
    }
}
