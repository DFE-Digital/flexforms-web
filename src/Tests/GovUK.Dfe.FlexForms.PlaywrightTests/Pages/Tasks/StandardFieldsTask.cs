using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Tasks;

public sealed class StandardFieldsTask(IPage page) : TaskPage(page)
{
    public const string TopicsPage = "topics-page";
    public const string GovernanceDetailsPage = "governance-details-page";
    public const string EducationDetailsPage = "education-details-page";
    public const string RegionPage = "region-page";

    public const string Topics = "topics";
    public const string ProposedDate = "proposedDate";

    /// <summary>The topics <see cref="AnswerAllQuestionsAsync"/> ticks, in the order the summaries list them.</summary>
    public static readonly string[] AnsweredTopics = ["Governance", "Education"];

    public const string AnsweredProposedDate = "05-11-2025";

    protected override string TaskItem => "group-standard-fields-task-complete-standard-fields";

    protected override string TaskId => "standard-fields";

    public async Task CompleteAsync()
    {
        await AnswerAllQuestionsAsync();
        await MarkCompleteAsync();
    }

    /// <summary>Answers every question and stops on the task summary, before marking it complete.</summary>
    public async Task AnswerAllQuestionsAsync()
    {
        await ByIdData("fullName").FillAsync("Test User");
        await SaveAndContinueAsync();
        await ByIdData("phoneNumber").FillAsync("07700 900123");
        await ByIdData("emailAddress").FillAsync("test@test.com");
        await SaveAndContinueAsync();
        await ByIdData("optionalReference").FillAsync("Optional Reference");
        await SaveAndContinueAsync();
        await ByIdData("additionalComments").FillAsync("Additional Comments \n with a new line");
        await SaveAndContinueAsync();
        await ByIdData("applicationReason").FillAsync("Application Reason");
        await SaveAndContinueAsync();
        await ConfirmYesAndContinueAsync();
        await ByIdData("followUpDetails").FillAsync("Follow Up Details");
        await SaveAndContinueAsync();
        await SelectTopicsAsync(AnsweredTopics);
        await GiveGovernanceDetailsAsync("Governance Details");
        await GiveEducationDetailsAsync("Education Details");
        await ByIdData("region").SelectOptionAsync("South West");
        await SaveAndContinueAsync();
        await EnterDateAsync("Data_proposedDate", "05", "11", "2025");
        await SaveAndContinueAsync();
        await ExpectSummaryAsync();
    }

    public async Task MarkCompleteAsync() => await MarkCompleteAndSaveAsync();

    public async Task SelectTopicsAsync(params string[] topics)
    {
        foreach (var topic in topics)
        {
            await TickAsync(topic);
        }

        await SaveAndContinueAsync();
    }

    public async Task GiveGovernanceDetailsAsync(string details)
    {
        await ByIdData("governanceDetails").FillAsync(details);
        await SaveAndContinueAsync();
    }

    public async Task GiveEducationDetailsAsync(string details)
    {
        await ByIdData("educationDetails").FillAsync(details);
        await SaveAndContinueAsync();
    }
}
