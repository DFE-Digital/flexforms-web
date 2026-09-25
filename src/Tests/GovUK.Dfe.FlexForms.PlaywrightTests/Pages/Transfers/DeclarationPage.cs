using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Transfers;

public sealed class DeclarationPage(IPage page) : TaskPage(page)
{
    protected override string TaskItem => "group-declaration-task-declaration-from-all-chairs-of-trustees";

    public async Task CompleteAsync()
    {
        // Joining academy declaration
        await Page.Locator("a[href*=\"trust-declarations-joining\"]").First.ClickAsync();
        await ById("Data_equalities-duties-decision_").CheckAsync();
        await ById("Data_chairName-joining").FillAsync("John Cena");
        await EnterDateAsync("Data_dateSigned-joining", "11", "11", "2025");
        await SaveAndContinueAsync();

        // Leaving academy declaration
        await Page.Locator("a[href*=\"trust-declarations-leaving\"]").First.ClickAsync();
        await ById("Data_equalities-duties-decision-leaving_-2").CheckAsync();
        await ById("Data_chairName-leaving").FillAsync("Michelle Loner");
        await EnterDateAsync("Data_dateSigned-leaving", "20", "01", "2026");
        await SaveAndContinueAsync();

        await MarkCompleteAndSaveAsync();
    }
}
