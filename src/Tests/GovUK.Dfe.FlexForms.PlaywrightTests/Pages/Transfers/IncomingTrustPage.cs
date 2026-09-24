using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Transfers;

public sealed record ContactDetails(string Name, string Phone, string Email);

public sealed class IncomingTrustPage(IPage page) : TaskPage(page)
{
    private const string SearchInput = "Data_incomingTrustsSearch-field-flow-complex-field";
    private const string UploadField = "incomingTrustUploadBoardResolution";

    protected override string TaskItem => "group-about-the-trust-that-academies-are-joining-task-trust-details";

    public async Task CompleteAsync(string trustName)
    {
        await ById("detailsOfIncomingTrust-add-item").ClickAsync();

        await SearchAutocompleteAsync(SearchInput, trustName);
        await ConfirmYesAndContinueAsync();

        // What is the type of trust? -> Single academy trust (first option)
        await ById("Data_incomingTrustTypeOfTrust_").CheckAsync();
        await SaveAndContinueAsync();

        await EnterContactAsync("incomingTrustAccountingOfficer", new ContactDetails("Test Officer", "0123456789", "officer@gov.uk"));
        await EnterContactAsync("incomingTrustChiefFinancialOfficer", new ContactDetails("Finance Officer", "0987654321", "finance@gov.uk"));
        await EnterContactAsync("incomingTrustChairOfTrustee", new ContactDetails("Chair Trustee", "0987654321", "chair@gov.uk"));

        // Main contact has an additional Role field
        await ById("Data_incomingTrustMainContactFullName").FillAsync("Main Contact");
        await ById("Data_incomingTrustMainContactRole").FillAsync("Director");
        await ById("Data_incomingTrustMainContactPhoneNumber").FillAsync("0123456789");
        await ById("Data_incomingTrustMainContactEmailAddress").FillAsync("main@gov.uk");
        await SaveAndContinueAsync();

        await UploadFileAsync(UploadField);

        await MarkCompleteAndSaveAsync();
    }

    private async Task EnterContactAsync(string fieldPrefix, ContactDetails contact)
    {
        await ById($"Data_{fieldPrefix}FullName").FillAsync(contact.Name);
        await ById($"Data_{fieldPrefix}PhoneNumber").FillAsync(contact.Phone);
        await ById($"Data_{fieldPrefix}EmailAddress").FillAsync(contact.Email);
        await SaveAndContinueAsync();
    }
}
