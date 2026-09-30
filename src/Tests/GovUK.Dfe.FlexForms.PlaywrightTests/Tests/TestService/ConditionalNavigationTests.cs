using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using GovUK.Dfe.FlexForms.PlaywrightTests.Api.Builders;
using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Tasks;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Tests.TestService;

[TestFixture(Description = "Conditional navigation")]
public sealed class ConditionalNavigationTests : PlaywrightTestBase
{
    [SetUp]
    public async Task LoginAsDefaultUserAsync() => await LoginAsync();

    [TestCase(TestName =
        "a question that returns to the summary lists its follow-up there and blocks completion until it is answered")]
    [CiRetry]
    public async Task ReturnToSummaryQuestionListsFollowUpAsync()
    {
        var task = new ConditionalNavigationTask(Page);
        await task.GoToAsync(await CreateApplicationAsync(), ConditionalNavigationTask.ContactPreferencesQuestionPage);

        await task.AnswerContactPreferencesQuestionAsync("Yes");
        await task.ExpectSummaryAsync();
        await task.ExpectListedAsync(ConditionalNavigationTask.ContactPreferences);

        await task.ChangeAnswerAsync(ConditionalNavigationTask.NotesQuestion);
        await task.AnswerNotesQuestionAsync("No");
        await task.ChangeAnswerAsync(ConditionalNavigationTask.ClosingQuestion);
        await task.AnswerClosingQuestionAsync("No");

        await task.TryMarkCompleteAsync();
        await task.ExpectCompletionBlockedByAsync("Enter your contact preferences");

        await task.ChangeAnswerAsync(ConditionalNavigationTask.ContactPreferencesQuestion);
        await task.AnswerContactPreferencesQuestionAsync("No");
        await task.ExpectNotListedAsync(ConditionalNavigationTask.ContactPreferences);
        await task.MarkCompleteAsync();
        await task.ExpectCompletedAsync();
    }

    [TestCase(TestName = "a branch question goes straight to its follow-up, and no returns to the summary")]
    [CiRetry]
    public async Task BranchQuestionGoesToFollowUpAsync()
    {
        var task = new ConditionalNavigationTask(Page);
        await task.GoToAsync(await CreateApplicationAsync(), ConditionalNavigationTask.NotesQuestionPage);

        await task.AnswerNotesQuestionAsync("Yes");
        await task.ExpectPageAsync(ConditionalNavigationTask.NotesPage);
        await task.AddNotesAsync("Some notes");
        await task.ExpectSummaryAsync();

        await task.ChangeAnswerAsync(ConditionalNavigationTask.NotesQuestion);
        await task.AnswerNotesQuestionAsync("No");
        await task.ExpectSummaryAsync();
        await task.ExpectNotListedAsync(ConditionalNavigationTask.Notes);
    }

    [TestCase(TestName = "no to the last question in a task returns to that task's summary, not the next task")]
    [CiRetry]
    public async Task LastQuestionNoReturnsToOwnSummaryAsync()
    {
        var task = new ConditionalNavigationTask(Page);
        await task.GoToAsync(await CreateApplicationAsync(), ConditionalNavigationTask.ClosingQuestionPage);

        await task.AnswerClosingQuestionAsync("No");
        await task.ExpectSummaryAsync();

        await task.ChangeAnswerAsync(ConditionalNavigationTask.ClosingQuestion);
        await task.AnswerClosingQuestionAsync("Yes");
        await task.ExpectPageAsync(ConditionalNavigationTask.ClosingDetailsPage);
        await task.AddClosingDetailsAsync("Closing details");
        await task.ExpectSummaryAsync();
    }

    [TestCase(TestName = "ticking two topics shows both follow-up pages in turn")]
    [CiRetry]
    public async Task TwoTopicsShowBothFollowUpsAsync()
    {
        var task = new StandardFieldsTask(Page);
        await task.GoToAsync(await CreateApplicationAsync(), StandardFieldsTask.TopicsPage);

        await task.SelectTopicsAsync("Governance", "Education");
        await task.ExpectPageAsync(StandardFieldsTask.GovernanceDetailsPage);
        await task.GiveGovernanceDetailsAsync("Governance details");
        await task.ExpectPageAsync(StandardFieldsTask.EducationDetailsPage);
        await task.GiveEducationDetailsAsync("Education details");
        await task.ExpectPageAsync(StandardFieldsTask.RegionPage);
    }

    [TestCase(TestName = "a topic without a follow-up goes straight to the next question")]
    [CiRetry]
    public async Task TopicWithoutFollowUpSkipsAheadAsync()
    {
        var task = new StandardFieldsTask(Page);
        await task.GoToAsync(await CreateApplicationAsync(), StandardFieldsTask.TopicsPage);

        await task.SelectTopicsAsync("Finance");
        await task.ExpectPageAsync(StandardFieldsTask.RegionPage);
    }

    [TestCase("New", AddSampleItemsTask.DescriptionPage, TestName = "a new sample item is asked for a description")]
    [TestCase("Existing", AddSampleItemsTask.DetailsPage, TestName = "an existing sample item skips the description")]
    [CiRetry]
    public async Task SampleItemStatusDecidesNextPageAsync(string status, string expectedPage)
    {
        var task = new AddSampleItemsTask(Page);
        await task.GoToAsync(await CreateApplicationAsync());

        await task.StartNewItemAsync();
        await task.AnswerItemStatusAsync(status);
        await task.ExpectItemPageAsync(expectedPage);
    }

    private async Task<string> CreateApplicationAsync()
    {
        var application = await ApplicationApi.CreateApplicationAsync(
            ApiClient,
            ApplicationBuilder.CreateApplicationRequest(ApiConfig.TemplateId));

        return application.ApplicationReference;
    }
}