using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Transfers;

public sealed class TrusteesPage(IPage page) : TaskPage(page)
{
    protected override string TaskItem => "group-about-the-trust-that-academies-are-joining-task-trustees";

    public async Task AddExistingTrusteeAsync(string name, string futureRoles, bool localGoverningBody)
    {
        await ById("trusteesAfterTransfer-add-item").ClickAsync();
        await SaveAndContinueAsync();
        await ById("Data_trusteeName").First.FillAsync(name);
        await SaveAndContinueAsync();
        await ById("Data_existingTrustee_").First.CheckAsync();
        await SaveAndContinueAsync();
        await ById("Data_trusteeFutureRoles").FillAsync(futureRoles);
        await SaveAndContinueAsync();
        await ById(localGoverningBody ? "Data_trusteeLocalGoverningBody_" : "Data_trusteeLocalGoverningBody_-2").CheckAsync();
        await SaveAndContinueAsync();
    }

    public async Task AddNewTrusteeAsync(string name, string pastRoles, bool localGoverningBody, string futureRoles)
    {
        await ById("trusteesAfterTransfer-add-item").ClickAsync();
        await SaveAndContinueAsync();
        await ById("Data_trusteeName").First.FillAsync(name);
        await SaveAndContinueAsync();
        await ById("Data_existingTrustee_-2").First.CheckAsync();
        await SaveAndContinueAsync();
        await ById("Data_trusteePastRoles").FillAsync(pastRoles);
        await SaveAndContinueAsync();
        await ById("Data_trusteeFutureRoles").FillAsync(futureRoles);
        await SaveAndContinueAsync();
        await ById(localGoverningBody ? "Data_trusteeLocalGoverningBody_" : "Data_trusteeLocalGoverningBody_-2").CheckAsync();
        await SaveAndContinueAsync();
    }

    public async Task AddLeavingTrusteeAsync(string name)
    {
        await ById("trusteesLeaving-add-item").ClickAsync();
        await ById("Data_trusteeLeavingName").First.FillAsync(name);
        await SaveAndContinueAsync();
    }

    public async Task CompleteAsync() => await MarkCompleteAndSaveAsync();
}
