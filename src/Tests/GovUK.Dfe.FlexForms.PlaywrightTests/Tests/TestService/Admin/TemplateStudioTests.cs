using System.Text.Json.Nodes;
using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using GovUK.Dfe.FlexForms.PlaywrightTests.Api.Builders;
using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;
using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using static GovUK.Dfe.FlexForms.PlaywrightTests.Api.Builders.StudioTemplateBuilder;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Tests.TestService.Admin;

[TestFixture(Description = "Template Studio")]
[NonParallelizable] // Saves new versions of the "template studio" template
public sealed class TemplateStudioTests : PlaywrightTestBase
{
    private string _templateId = null!;

    /// <summary>
    /// Templates can't be deleted, so the tests reuse one template, created the first time they run.
    /// Each test that needs it starts from the known version.
    /// </summary>
    [OneTimeSetUp]
    public async Task CreateStudioTemplateAsync()
    {
        var template = await Templates.FindTemplateByNameAsync(AdminApiClient, TestData.TemplateStudioTemplateName)
                       ?? await Templates.CreateTemplateAsync(AdminApiClient, TestData.TemplateStudioTemplateName);
        _templateId = template.TemplateId;
    }

    [SetUp]
    public async Task LoginAsAdminAsync() => await LoginAsync("admin");

    private async Task<string> ResetStudioTemplateAsync()
    {
        var template = await Templates.FindTemplateByNameAsync(AdminApiClient, TestData.TemplateStudioTemplateName);
        var version = NextVersion(template?.LatestVersionNumber);
        await Templates.CreateTemplateVersionAsync(AdminApiClient, _templateId, version,
            Schema(_templateId, TestData.TemplateStudioTemplateName));
        return version;
    }

    private async Task<TemplateStudio> OpenStudioAsync()
    {
        await ResetStudioTemplateAsync();
        var studio = new TemplateStudio(Page);
        await studio.OpenAsync(_templateId);
        await studio.ExpectOpenAsync(TestData.TemplateStudioTemplateName);
        return studio;
    }

    [TestCase(TestName = "Admin can open a template in Template Studio and find no problems")]
    [CiRetry]
    public async Task AdminCanOpenAndCheckTemplateAsync()
    {
        var studio = await OpenStudioAsync();

        await studio.CheckForProblemsAsync();

        await studio.ExpectNoProblemsAsync();
    }

    [TestCase(TestName = "Admin can add a question and save it as a new version")]
    [CiRetry]
    public async Task AdminCanAddQuestionAndSaveAsync()
    {
        var studio = await OpenStudioAsync();
        await studio.ExpandAsync(DetailsTask);
        await studio.SelectAsync("page", NotesPage);

        await studio.AddQuestionAsync("Short answer", "Reference number");
        await studio.ExpectUnsavedChangesAsync();
        var version = await studio.SaveNewVersionAsync();

        await studio.ExpectSavedAsync(version);
        var saved = JsonNode.Parse(await Templates.GetLatestSchemaJsonAsync(AdminApiClient, _templateId))!;
        Assert.That(saved.ToJsonString(), Does.Contain("\"fieldId\":\"referenceNumber\""));
    }

    [TestCase(TestName = "Undo takes a change back and redo puts it back")]
    [CiRetry]
    public async Task UndoAndRedoAsync()
    {
        var studio = await OpenStudioAsync();
        await studio.ExpandAsync(DetailsTask);
        await studio.SelectAsync("page", NotesPage);
        await studio.AddQuestionAsync("Short answer", "Reference number");

        await studio.UndoAsync();
        await studio.ExpectQuestionNotInStructureAsync("Reference number");

        await studio.RedoAsync();
        await studio.ExpectQuestionInStructureAsync("Reference number");
    }

    [TestCase(TestName = "Saving stops when someone else saved a newer version, and can then save over it")]
    [CiRetry]
    public async Task SavingStopsWhenSomeoneElseSavedAsync()
    {
        var studio = await OpenStudioAsync();
        await studio.ExpandAsync(DetailsTask);
        await studio.SelectAsync("page", NotesPage);
        await studio.AddQuestionAsync("Short answer", "Reference number");

        var newerVersion = await ResetStudioTemplateAsync();
        await studio.SaveNewVersionAsync();
        await studio.ExpectNewerVersionErrorAsync(newerVersion);

        var version = await studio.SaveNewVersionAsync(NextVersion(newerVersion), saveOverNewerVersion: true);
        await studio.ExpectSavedAsync(version);
    }

    [TestCase(TestName = "A rule with more than one condition shows a page only when its conditions are met")]
    [CiRetry]
    public async Task RuleShowsPageWhenConditionsAreMetAsync()
    {
        const string ruleName = "Ask for the trust name";
        var studio = await OpenStudioAsync();

        await studio.StartRuleAsync();
        await studio.NameRuleAsync(ruleName);
        await studio.SetConditionAsync(0, HasTrustQuestionId, "is", "yes");
        await studio.AddConditionAsync();
        await studio.SetConditionAsync(1, HasTrustQuestionId, "has been given");
        await studio.ChooseWhichConditionsAsync("All of them");
        await studio.ShowPageAsync(TrustNamePage);
        await studio.SaveRuleAsync();
        await studio.ExpectRuleAsync(ruleName);

        var preview = await studio.OpenPreviewAsync();
        await preview.ExpectOpenAsync();
        await preview.GoToTaskListAsync();
        await preview.OpenTaskAsync(DetailsTask);
        await preview.StartAsync();
        await preview.ExpectPageAsync(HasTrustPage);
        await preview.ChooseAsync("No");
        await preview.SaveAndContinueAsync();
        await preview.ExpectPageAsync(NotesPage);

        await preview.GoBackAsync();
        await preview.ChooseAsync("Yes");
        await preview.SaveAndContinueAsync();
        await preview.ExpectPageAsync(TrustNamePage);
    }

    [TestCase(TestName = "The rule builder says what's missing before it saves a rule")]
    [CiRetry]
    public async Task RuleBuilderShowsWhatIsMissingAsync()
    {
        var studio = await OpenStudioAsync();

        await studio.StartRuleAsync();
        await studio.SaveRuleAsync();

        await studio.ExpectRuleErrorAsync("Choose the question for condition 1");
        await studio.ExpectRuleErrorAsync("Choose at least one page or question to show");
    }

    [TestCase(TestName = "A rule added in the JSON tab appears on the Rules tab and can be changed with the rule builder")]
    [CiRetry]
    public async Task RuleAddedInJsonAppearsOnRulesTabAsync()
    {
        const string ruleName = "Show the trust name page";
        var studio = await OpenStudioAsync();

        await studio.EditJsonAsync(json => WithHandWrittenTrustNameRules(json, ruleName));
        await studio.OpenTabAsync("Rules");

        await studio.ExpectRuleAsync(ruleName);
        await studio.ExpectRuleCanBeChangedAsync(ruleName);
        await studio.ExpectUnsavedChangesAsync();
    }

    [TestCase(TestName = "Task list details group a repeatable list's questions under the list, and can't show the whole list")]
    [CiRetry]
    public async Task TaskListDetailsGroupListQuestionsAsync()
    {
        var studio = await OpenStudioAsync();
        await studio.SelectTemplateSettingsAsync(TestData.TemplateStudioTemplateName);

        await studio.StartAddingTaskListDetailAsync();

        await studio.ExpectQuestionGroupedUnderListAsync($"{ContactsTask}: {ContactsList} (repeatable list)", ContactNameQuestion);
        await studio.ExpectNotAChoiceAsync(ContactsListId);
    }

    [TestCase(TestName = "The preview checks answers, keeps them and lets the applicant mark the section complete")]
    [CiRetry]
    public async Task PreviewChecksAndKeepsAnswersAsync()
    {
        var studio = await OpenStudioAsync();
        var preview = await studio.OpenPreviewAsync();
        await preview.ExpectOpenAsync();
        await preview.GoToTaskListAsync();
        await preview.OpenTaskAsync(DetailsTask);
        await preview.StartAsync();

        await preview.SaveAndContinueAsync();
        await preview.ExpectErrorAsync(HasTrustRequiredMessage);

        await preview.ChooseAsync("Yes");
        await preview.SaveAndContinueAsync();
        await preview.ExpectPageAsync(TrustNamePage);
        await preview.FillAsync(TrustNameQuestionId, "Oak Trust");
        await preview.SaveAndContinueAsync();
        await preview.ExpectPageAsync(NotesPage);
        await preview.SaveAndContinueAsync();

        await preview.ExpectAnswerAsync(TrustNamePage, "Oak Trust");
        await preview.MarkCompleteAsync();
        await preview.ExpectTaskStatusAsync(DetailsTask, "Completed");
    }

    [TestCase(TestName = "The preview adds and removes items in a repeatable list and checks the fewest items")]
    [CiRetry]
    public async Task PreviewAddsAndRemovesListItemsAsync()
    {
        var studio = await OpenStudioAsync();
        var preview = await studio.OpenPreviewAsync();
        await preview.ExpectOpenAsync();
        await preview.GoToTaskListAsync();
        await preview.OpenTaskAsync(ContactsTask);

        await preview.MarkCompleteAsync();
        await preview.ExpectCompletionBlockedByAsync($"Add at least 1 item(s) to {ContactsList}");

        await preview.AddItemAsync(AddContactButton);
        await preview.ExpectPageAsync(ContactPage);
        await preview.FillAsync(ContactNameQuestionId, "Ann");
        await preview.SaveAndContinueAsync();
        await preview.ExpectSuccessAsync("Ann has been added");
        await preview.ExpectItemAsync("Ann");

        await preview.RemoveItemAsync("Ann");
        await preview.ExpectSuccessAsync("Ann has been removed");
        await preview.ExpectNoItemAsync("Ann");
    }

    // Two rules written by hand, as older templates have them: one skips the page unless the answer is yes, one shows it.
    private static string WithHandWrittenTrustNameRules(string json, string ruleName)
    {
        var template = JsonNode.Parse(json)!;
        JsonNode Rule(string id, string name, int priority, string @operator, string action) => new JsonObject
        {
            ["id"] = id,
            ["name"] = name,
            ["priority"] = priority,
            ["enabled"] = true,
            ["conditionGroup"] = new JsonObject
            {
                ["logicalOperator"] = "AND",
                ["conditions"] = new JsonArray(new JsonObject
                {
                    ["triggerField"] = HasTrustQuestionId,
                    ["operator"] = @operator,
                    ["value"] = "yes",
                    ["dataType"] = "string"
                })
            },
            ["affectedElements"] = new JsonArray(new JsonObject
            {
                ["elementId"] = "trust-name",
                ["elementType"] = "page",
                ["action"] = action
            })
        };
        template["conditionalLogic"] = new JsonArray(
            Rule("hide-trust-name-unless-yes", "Hide the trust name page", 60, "notEquals", "skip"),
            Rule("show-trust-name-for-yes", ruleName, 61, "equals", "show"));
        return template.ToJsonString();
    }
}
