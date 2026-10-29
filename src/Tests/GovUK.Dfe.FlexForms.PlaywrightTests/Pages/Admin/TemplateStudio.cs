using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;

public sealed class TemplateStudio(IPage page) : BasePage(page)
{
    public async Task OpenAsync(string templateId)
    {
        await Page.GotoAsync("/admin/template-studio");
        await ById("SelectedTemplateId").SelectOptionAsync(templateId.ToLowerInvariant());
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Open template" }).ClickAsync();
    }

    public async Task ExpectOpenAsync(string templateName)
    {
        await Assertions.Expect(Page).ToHaveURLAsync(new Regex("/admin/template-studio$"));
        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = templateName, Level = 1 }))
            .ToBeVisibleAsync();
        await Assertions.Expect(Status()).ToHaveTextAsync("No unsaved changes");
    }

    // ---- Structure -------------------------------------------------------------------------------------------------

    /// <summary>Opens sections, tasks and pages in the structure by name, in order.</summary>
    public async Task ExpandAsync(params string[] names)
    {
        foreach (var name in names)
        {
            var summary = Structure().Locator("summary").Filter(new LocatorFilterOptions
            {
                Has = Page.GetByText(name, new PageGetByTextOptions { Exact = true })
            }).First;
            if (await summary.Locator("xpath=..").GetAttributeAsync("open") is null)
                await summary.ClickAsync();
        }
    }

    /// <summary>Selects a section, task, page or list. <paramref name="kind"/> is how the structure names it, for example "page".</summary>
    public async Task SelectAsync(string kind, string title)
    {
        await ExpandAsync(title);
        await Structure().GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = $"Edit {kind} {title}", Exact = true }).ClickAsync();
        await ExpectSelectedAsync(title);
    }

    public async Task SelectQuestionAsync(string question)
    {
        await Structure().GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = question, Exact = true }).ClickAsync();
        await ExpectSelectedAsync(question);
    }

    public async Task SelectTemplateSettingsAsync(string templateName)
    {
        await Structure().GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = templateName, Exact = true }).ClickAsync();
        await ExpectSelectedAsync(templateName);
    }

    public async Task ExpectSelectedAsync(string title) =>
        await Assertions.Expect(ById("tpl-inspector-title")).ToHaveTextAsync(title);

    public async Task ExpectQuestionInStructureAsync(string question) =>
        await Assertions.Expect(QuestionLink(question)).ToBeVisibleAsync();

    public async Task ExpectQuestionNotInStructureAsync(string question) =>
        await Assertions.Expect(QuestionLink(question)).ToHaveCountAsync(0);

    /// <summary>Adds a question to the selected page.</summary>
    public async Task AddQuestionAsync(string type, string question)
    {
        await ById("tpl-new-field-type").SelectOptionAsync(new SelectOptionValue { Label = type });
        await ById("tpl-new-field-label").FillAsync(question);
        await ById("tpl-new-field-button").ClickAsync();
        await ExpectSelectedAsync(question);
    }

    public async Task ExpectUnsavedChangesAsync() =>
        await Assertions.Expect(Status()).ToContainTextAsync("Unsaved changes");

    public async Task UndoAsync() => await ToolbarButton("Undo").ClickAsync();

    public async Task RedoAsync() => await ToolbarButton("Redo").ClickAsync();

    // ---- Deleting --------------------------------------------------------------------------------------------------

    public async Task StartDeletingAsync() => await ById("tpl-delete").ClickAsync();

    public async Task ExpectRetireWarningAsync() =>
        await Assertions.Expect(ById("tpl-delete-confirm")).ToContainTextAsync("Applications already have answers to this question");

    public async Task CancelDeletingAsync() =>
        await ById("tpl-delete-confirm").GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Cancel" }).ClickAsync();

    // ---- Template settings -----------------------------------------------------------------------------------------

    public async Task StartAddingTaskListDetailAsync()
    {
        if (await ById("tpl-metadata-customise").CountAsync() > 0)
            await ById("tpl-metadata-customise").ClickAsync();
        await ById("tpl-add-detail").ClickAsync();
    }

    /// <summary>The detail's answer choices list <paramref name="question"/> under the heading for its repeatable list.</summary>
    public async Task ExpectQuestionGroupedUnderListAsync(string listHeading, string question) =>
        await Assertions.Expect(DetailChoices().Locator($"optgroup[label=\"{listHeading}\"] option").Filter(new LocatorFilterOptions { HasText = question }))
            .ToHaveCountAsync(1);

    public async Task ExpectNotAChoiceAsync(string answerId) =>
        await Assertions.Expect(DetailChoices().Locator($"option[value=\"{answerId}\"]")).ToHaveCountAsync(0);

    // ---- Rules -----------------------------------------------------------------------------------------------------

    public async Task OpenTabAsync(string tab) =>
        await Page.GetByRole(AriaRole.Tab, new PageGetByRoleOptions { Name = tab }).ClickAsync();

    public async Task StartRuleAsync()
    {
        await OpenTabAsync("Rules");
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add a rule" }).ClickAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Add a rule" })).ToBeVisibleAsync();
    }

    public async Task NameRuleAsync(string name) => await ById("tpl-rule-name").FillAsync(name);

    /// <summary>Sets condition <paramref name="index"/> (from 0). <paramref name="answer"/> is an option's saved value, or text.</summary>
    public async Task SetConditionAsync(int index, string questionId, string comparison, string? answer = null)
    {
        await ById($"tpl-rule-trigger-{index}").SelectOptionAsync(questionId);
        await ById($"tpl-rule-comparison-{index}").SelectOptionAsync(new SelectOptionValue { Label = comparison });
        if (answer is null)
            return;

        var value = ById($"tpl-rule-value-{index}");
        if (await value.EvaluateAsync<string>("element => element.tagName") == "SELECT")
            await value.SelectOptionAsync(answer);
        else
            await value.FillAsync(answer);
    }

    public async Task AddConditionAsync()
    {
        await ById("tpl-rule-add-condition").ClickAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Condition 2" })).ToBeVisibleAsync();
    }

    public async Task ChooseWhichConditionsAsync(string choice) =>
        await Page.GetByRole(AriaRole.Radio, new PageGetByRoleOptions { Name = choice, Exact = true }).CheckAsync();

    public async Task ShowPageAsync(string pageTitle) =>
        await Rules().GetByRole(AriaRole.Checkbox, new LocatorGetByRoleOptions { Name = pageTitle, Exact = true }).CheckAsync();

    public async Task SaveRuleAsync() => await ById("tpl-rule-save").ClickAsync();

    public async Task ExpectRuleAsync(string name) =>
        await Assertions.Expect(RuleCard(name)).ToBeVisibleAsync();

    public async Task ExpectRuleCanBeChangedAsync(string name) =>
        await Assertions.Expect(RuleCard(name).GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = $"Change {name}" })).ToBeVisibleAsync();

    public async Task ExpectRuleErrorAsync(string message) =>
        await Assertions.Expect(Rules().Locator(".govuk-error-summary")).ToContainTextAsync(message);

    // ---- JSON ------------------------------------------------------------------------------------------------------

    public async Task EditJsonAsync(Func<string, string> change)
    {
        await OpenTabAsync("JSON (advanced)");
        var json = await ById("tpl-json").InputValueAsync();
        await ById("tpl-json").FillAsync(change(json));
    }

    // ---- Checking, previewing and saving ---------------------------------------------------------------------------

    public async Task CheckForProblemsAsync() => await ById("tpl-check-button").ClickAsync();

    public async Task ExpectNoProblemsAsync() =>
        await Assertions.Expect(ById("tpl-problems")).ToContainTextAsync("No problems found. This template can be saved.");

    public async Task<TemplateStudioPreview> OpenPreviewAsync()
    {
        var preview = await Page.Context.RunAndWaitForPageAsync(() => ById("tpl-preview-button").ClickAsync());
        await preview.WaitForLoadStateAsync();
        return new TemplateStudioPreview(preview);
    }

    /// <summary>Saves a new version and returns its number. Leave <paramref name="version"/> out to use the suggested number.</summary>
    public async Task<string> SaveNewVersionAsync(string? version = null, bool saveOverNewerVersion = false)
    {
        if (version is not null)
            await ById("NewVersion").FillAsync(version);
        var number = await ById("NewVersion").InputValueAsync();
        await ById("AcknowledgeReportingImpact").CheckAsync();
        if (saveOverNewerVersion)
            await ById("SaveOverNewerVersion").CheckAsync();
        await ById("tpl-save-button").ClickAsync();
        return number;
    }

    public async Task ExpectSavedAsync(string version) =>
        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = $"Version {version} saved" })).ToBeVisibleAsync();

    public async Task ExpectNewerVersionErrorAsync(string newerVersion) =>
        await Assertions.Expect(Page.Locator(".govuk-error-summary"))
            .ToContainTextAsync($"Version {newerVersion} was saved after you opened Template Studio");

    private ILocator Structure() => ById("tpl-tree");

    private ILocator Rules() => ById("tpl-rules");

    private ILocator Status() => ById("tpl-status");

    private ILocator DetailChoices() => ById("tpl-detail-field");

    private ILocator QuestionLink(string question) =>
        Structure().GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = question, Exact = true });

    private ILocator ToolbarButton(string name) =>
        Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = name, Exact = true });

    private ILocator RuleCard(string name) =>
        Rules().Locator(".govuk-summary-card").Filter(new LocatorFilterOptions
        {
            Has = Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = name, Exact = true })
        });
}
