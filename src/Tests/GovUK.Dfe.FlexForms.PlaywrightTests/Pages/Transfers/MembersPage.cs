using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Transfers;

public sealed class MembersPage(IPage page) : TaskPage(page)
{
    protected override string TaskItem => "group-about-the-trust-that-academies-are-joining-task-members";

    public async Task AddExistingMemberAsync(string name)
    {
        await ExpectSummaryAsync();
        await ById("membersAfterTransfer-add-item").ClickAsync();
        await ById("Data_memberName").First.FillAsync(name);
        await SaveAndContinueAsync();
        await ById("Data_existingMember_").First.CheckAsync();
        await SaveAndContinueAsync();
        await ById("Data_additionalRoles_").CheckAsync();
        await SaveAndContinueAsync();
        await ExpectSummaryAsync();
    }

    public async Task AddNewMemberAsync(string name, string pastRoles)
    {
        await ExpectSummaryAsync();
        await ById("membersAfterTransfer-add-item").ClickAsync();
        await ById("Data_memberName").First.FillAsync(name);
        await SaveAndContinueAsync();
        await ById("Data_existingMember_-2").First.CheckAsync();
        await SaveAndContinueAsync();
        await ById("Data_pastRoles").First.FillAsync(pastRoles);
        await SaveAndContinueAsync();
        await ById("Data_additionalRoles_-2").CheckAsync();
        await SaveAndContinueAsync();
        await ExpectSummaryAsync();
    }

    public async Task AddLeavingMemberAsync(string name)
    {
        await ExpectSummaryAsync();
        await ById("membersLeaving-add-item").ClickAsync();
        await ById("Data_memberLeavingName").First.FillAsync(name);
        await SaveAndContinueAsync();
        await ExpectSummaryAsync();
    }

    public async Task CompleteAsync() => await MarkCompleteAndSaveAsync();

    private async Task ExpectSummaryAsync() => await Assertions.Expect(Page).ToHaveURLAsync(new Regex(@"/members$"));
}
