using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Transfers;

public sealed class OutgoingTrustPage(IPage page) : TaskPage(page)
{
    private const string SearchInput = "Data_trustsSearch-field-flow-complex-field";
    private const string UploadField = "outgoingTrustUploadBoardResolution";

    protected override string TaskItem => "group-about-the-trusts-that-academies-are-leaving-task-details-of-trusts";

    public async Task CompleteAsync(string trustName)
    {
        await ById("detailsOfOutgoingTrusts-add-item").ClickAsync();

        await SearchAutocompleteAsync(SearchInput, trustName);
        await ConfirmYesAndContinueAsync();

        await ById("Data_outgoingTrustContactDetailsFullName").FillAsync("Michael Scott");
        await ById("Data_outgoingTrustContactDetailsRole").FillAsync("Granting Officer");
        await ById("Data_outgoingTrustContactDetailsPhoneNumber").FillAsync("07700 900 982");
        await ById("Data_outgoingTrustContactDetailsEmailAddress").FillAsync("M.A@gov.uk");
        await SaveAndContinueAsync();

        // Will the trust close? -> Yes + upload board resolution
        await ById("Data_willTrustClose_").CheckAsync();
        await SaveAndContinueAsync();
        await UploadFileAsync(UploadField);
        await ExpectSummaryAsync();

        await MarkCompleteAndSaveAsync();
    }

    private async Task ExpectSummaryAsync()
    {
        await Assertions.Expect(Page).ToHaveURLAsync(new Regex(@"/details-of-outgoing-trusts$"));
        await Assertions.Expect(ById("IsTaskCompleted")).ToBeVisibleAsync();
    }
}
