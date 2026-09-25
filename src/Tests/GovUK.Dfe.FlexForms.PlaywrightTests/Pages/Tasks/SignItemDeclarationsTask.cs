using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Tasks;

public sealed class SignItemDeclarationsTask(IPage page) : TaskPage(page)
{
    protected override string TaskItem => "group-collection-flows-task-sign-item-declarations";

    public async Task CompleteAsync(string establishment)
    {
        await ById($"view-{establishment}").ClickAsync();

        await ByIdData("declarationName").FillAsync("Test User");
        await ByIdData("declarationAgreed_").CheckAsync();
        await EnterDateAsync("Data_declarationSignedDate", "1", "1", "2027");
        await SaveAndContinueAsync();

        await MarkCompleteAndSaveAsync();
    }
}
