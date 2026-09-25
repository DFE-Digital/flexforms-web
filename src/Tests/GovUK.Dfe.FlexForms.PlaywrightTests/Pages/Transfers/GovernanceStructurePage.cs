using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Transfers;

public sealed class GovernanceStructurePage(IPage page) : TaskPage(page)
{
    private const string UploadField = "governanceStructureAfterTheTransferPploadDocuments";

    protected override string TaskItem => "group-about-the-trust-that-academies-are-joining-task-governance-structure";

    public async Task CompleteAsync()
    {
        // Governance team confirmation -> No + explanation
        await ById("field-governanceteamconfirmation-change-link").ClickAsync();
        await ById("Data_governanceTeamConfirmation_-2").CheckAsync();
        await SaveAndContinueAsync();
        await ById("Data_governanceTeamExplanation").FillAsync("Governance structure testing text");
        await SaveAndContinueAsync();

        // Proposed governance structure -> upload document
        await ById("field-governancestructureafterthetransferpploaddocuments-change-link").ClickAsync();
        await UploadFileAsync(UploadField);

        await MarkCompleteAndSaveAsync();
    }
}
