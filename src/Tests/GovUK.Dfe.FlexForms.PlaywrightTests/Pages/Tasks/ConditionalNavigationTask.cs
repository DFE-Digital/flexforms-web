using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Tasks;

/// <summary>
/// "Answer conditional navigation questions": three questions, each with a follow-up page shown by answering yes.
/// The contact preferences question uses returnToSummaryPage true, the notes question navigationAfterSave branch,
/// and the closing question returnToSummaryPage false as the last question in the task.
/// </summary>
public sealed class ConditionalNavigationTask(IPage page) : TaskPage(page)
{
    public const string ContactPreferencesQuestionPage = "contact-preferences-question-page";
    public const string NotesQuestionPage = "notes-question-page";
    public const string NotesPage = "notes-page";
    public const string ClosingQuestionPage = "closing-question-page";
    public const string ClosingDetailsPage = "closing-details-page";

    public const string ContactPreferencesQuestion = "giveContactPreferences";
    public const string ContactPreferences = "contactPreferences";
    public const string NotesQuestion = "addNotes";
    public const string Notes = "notes";
    public const string ClosingQuestion = "addClosingDetails";

    protected override string TaskItem => "group-conditional-navigation-task-conditional-navigation-fields";

    protected override string TaskId => "conditional-navigation";

    /// <summary>The quickest valid route through the task: no to every question.</summary>
    public async Task CompleteAsync()
    {
        await AnswerContactPreferencesQuestionAsync("No");
        await ChangeAnswerAsync(NotesQuestion);
        await AnswerNotesQuestionAsync("No");
        await ChangeAnswerAsync(ClosingQuestion);
        await AnswerClosingQuestionAsync("No");
        await MarkCompleteAndSaveAsync();
    }

    public async Task AnswerContactPreferencesQuestionAsync(string answer) => await AnswerAsync(answer);

    public async Task GiveContactPreferencesAsync(string preferences) => await FillAndSaveAsync(ContactPreferences, preferences);

    public async Task AnswerNotesQuestionAsync(string answer) => await AnswerAsync(answer);

    public async Task AddNotesAsync(string notes) => await FillAndSaveAsync(Notes, notes);

    public async Task AnswerClosingQuestionAsync(string answer) => await AnswerAsync(answer);

    public async Task AddClosingDetailsAsync(string details) => await FillAndSaveAsync("closingDetails", details);

    public async Task TryMarkCompleteAsync() => await SubmitTaskCompletionAsync();

    public async Task MarkCompleteAsync() => await MarkCompleteAndSaveAsync();

    public async Task ExpectCompletionBlockedByAsync(string missingAnswerMessage) =>
        await Assertions.Expect(ErrorSummary()).ToContainTextAsync(missingAnswerMessage);

    private async Task AnswerAsync(string answer)
    {
        await ChooseAsync(answer);
        await SaveAndContinueAsync();
    }

    private async Task FillAndSaveAsync(string fieldId, string value)
    {
        await ByIdData(fieldId).FillAsync(value);
        await SaveAndContinueAsync();
    }

    private ILocator ErrorSummary() => Page.Locator(".govuk-error-summary");
}
