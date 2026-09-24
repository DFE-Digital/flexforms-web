using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Transfers;

public sealed class LeadershipPage(IPage page) : TaskPage(page)
{
    protected override string TaskItem => "group-about-the-trust-that-academies-are-joining-task-leadership-and-work-force";

    public async Task CompleteAsync()
    {
        // Will the leadership central team change? -> Yes + text
        await ById("field-leadershipandworkforcewilltheleadershipcentralteamchange-change-link").ClickAsync();
        await ById("Data_leadershipAndWorkForceWillTheLeadershipCentralTeamChange_").CheckAsync();
        await SaveAndContinueAsync();
        await ById("Data_leadershipAndWorkForceHowWillTheLeadershipCentralTeamChange").FillAsync(
            "Leadership and work force testing text");
        await SaveAndContinueAsync();

        await MarkCompleteAndSaveAsync();
    }
}
