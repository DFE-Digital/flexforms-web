using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Validation;
using GovUK.Dfe.FlexForms.Domain.FormEngine;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Web.Extensions;
using GovUK.Dfe.FlexForms.Web.Features;
using GovUK.Dfe.FlexForms.Web.Pages.FormEngine;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.FeatureManagement.Mvc;
using TaskModel = GovUK.Dfe.FlexForms.Domain.Models.Task;
using TemplatePage = GovUK.Dfe.FlexForms.Domain.Models.Page;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Lets a designer use an unsaved template as an applicant would: answer pages, see rules, checks and summaries.
/// The template and answers live in the session for this preview only; nothing is sent to the API.
/// </summary>
[Authorize(Policy = AdminAccessHelper.CanManageTemplatesPolicy)]
[FeatureGate(FeatureFlags.TemplateStudio)]
[RequestSizeLimit(TemplateStudioRequestLimits.MaxBytes)]
[RequestFormLimits(ValueLengthLimit = TemplateStudioRequestLimits.MaxBytes, ValueCountLimit = 1000)]
public partial class TemplateStudioPreviewModel(
    ITemplateValidationService templateValidationService,
    IDraftFormPreview draftFormPreview,
    IDerivedCollectionFlowService derivedCollectionFlowService,
    IComplexFieldConfigurationService complexFieldConfigurationService) : PageModel
{
    public const string SessionKey = "TemplateStudioPreview";

    // Matches how the form engine reads templates (JsonFormTemplateParser).
    private static readonly JsonSerializerOptions ParseOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    public enum PreviewView
    {
        NoPreview,
        TaskList,
        TaskSummary,
        Page,
        AllPages
    }

    [BindProperty]
    public string? Schema { get; set; }

    [BindProperty(SupportsGet = true, Name = "task")]
    public string? TaskId { get; set; }

    [BindProperty(SupportsGet = true, Name = "pageId")]
    public string? PageId { get; set; }

    [BindProperty(SupportsGet = true, Name = "list")]
    public string? ListId { get; set; }

    [BindProperty(SupportsGet = true, Name = "derived")]
    public string? DerivedId { get; set; }

    [BindProperty(SupportsGet = true, Name = "item")]
    public string? ItemId { get; set; }

    [BindProperty(SupportsGet = true, Name = "new")]
    public bool IsNewItem { get; set; }

    [BindProperty(SupportsGet = true, Name = "view")]
    public string? ViewName { get; set; }

    [BindProperty]
    public bool IsTaskCompleted { get; set; }

    [TempData]
    public string? Message { get; set; }

    public PreviewView View { get; private set; } = PreviewView.NoPreview;
    public string? ParseError { get; private set; }
    public FormTemplate? Template { get; private set; }
    public TaskModel? Task { get; private set; }
    public TemplatePage? CurrentPage { get; private set; }
    public MultiCollectionFlowConfiguration? List { get; private set; }
    public DerivedCollectionFlowConfiguration? Derived { get; private set; }
    public string? ItemName { get; private set; }
    public Dictionary<string, object> Data { get; private set; } = [];
    public Dictionary<string, object> Answers => _state?.Answers ?? [];
    public IReadOnlyList<FormValidationError> Errors { get; private set; } = [];
    public IReadOnlyList<string> CompletionProblems { get; private set; } = [];
    public IReadOnlyList<string> Problems { get; private set; } = [];
    public IReadOnlyList<PreviewPageSet> PageSets { get; private set; } = [];
    public IReadOnlySet<string> RuleTargets { get; private set; } = new HashSet<string>();
    public FormEngineVisibilityEvaluator? Visibility { get; private set; }

    private PreviewState? _state;

    public bool IsCompleted(string taskId) => _state?.Completed.Contains(taskId) == true;

    public IActionResult OnPost()
    {
        Schema = TemplateStudioSchemaTransport.Decode(Schema);
        if (string.IsNullOrWhiteSpace(Schema))
        {
            ParseError = "There's no template to preview. Go back to Template Studio and try again.";
            return Page();
        }

        if (!TryParse(Schema, out var template, out var error))
        {
            ParseError = error;
            return Page();
        }

        SaveState(new PreviewState { Schema = Schema });
        var task = AllTasks(template).FirstOrDefault(item => item.TaskId == TaskId);
        return RedirectToPage(new { task = task?.TaskId });
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!LoadState())
            return Page();

        if (Task is null)
        {
            View = PreviewView.TaskList;
            return Page();
        }

        if (string.Equals(ViewName, "all", StringComparison.OrdinalIgnoreCase))
        {
            View = PreviewView.AllPages;
            return Page();
        }

        if (string.IsNullOrEmpty(PageId))
        {
            var start = FirstPageWhenNotStarted();
            if (start is not null)
                return RedirectToPage(new { task = Task.TaskId, pageId = start });

            await ShowSummaryAsync();
            return Page();
        }

        if (!ResolvePage())
        {
            await ShowSummaryAsync();
            return Page();
        }

        Data = DataForPage();
        var conditionalState = await draftFormPreview.EvaluateAsync(Template!, Data, Task.TaskId, CurrentPage!.PageId);
        Visibility = draftFormPreview.Visibility(Template!, conditionalState, Task.TaskId, CurrentPage.PageId);
        View = PreviewView.Page;
        return Page();
    }

    public async Task<IActionResult> OnPostAnswerAsync()
    {
        if (!LoadState())
            return Page();
        if (Task is null || !ResolvePage())
            return RedirectToPage(new { task = Task?.TaskId });

        var posted = new Dictionary<string, IReadOnlyList<string>>(Request.Form.ToPostedFields(), StringComparer.Ordinal);
        var samples = new Dictionary<string, string>();
        foreach (var field in CurrentPage!.Fields ?? [])
        {
            if (field.Type == "complexField" && posted.TryGetValue($"Data[{field.FieldId}]", out var sample))
            {
                samples[field.FieldId] = SampleAnswerJson(field, sample.FirstOrDefault());
                posted[$"Data[{field.FieldId}]"] = [samples[field.FieldId]];
            }
        }

        var isTaskPage = List is null && Derived is null;
        var outcome = await draftFormPreview.SubmitPageAsync(Template!, Task, CurrentPage, PageSet(), isTaskPage, DataForPage(), posted);
        // Binding HTML-encodes posted text. Sample answers are JSON this page wrote, so they are kept as written.
        foreach (var (fieldId, json) in samples)
            outcome.Data[fieldId] = json;
        if (!outcome.IsValid)
        {
            Data = outcome.Data;
            Errors = outcome.Errors;
            Visibility = draftFormPreview.Visibility(Template!, outcome.ConditionalState, Task.TaskId, CurrentPage.PageId);
            View = PreviewView.Page;
            return Page();
        }

        if (List is not null)
            return SaveListItem(outcome);
        if (Derived is not null)
            return SaveDerivedItem(outcome);

        _state!.Answers = outcome.Data;
        SaveState(_state);
        return outcome.NextPageId is null
            ? RedirectToPage(new { task = Task.TaskId })
            : RedirectToPage(new { task = Task.TaskId, pageId = outcome.NextPageId });
    }

    public IActionResult OnPostRemove()
    {
        if (!LoadState())
            return Page();
        List = Task?.Summary?.Flows?.FirstOrDefault(flow => flow.FlowId == ListId);
        if (Task is null || List is null)
            return RedirectToPage(new { task = Task?.TaskId });

        var items = FormEngineCollectionItems.Read(_state!.Answers, List.FieldId);
        var removed = items.FirstOrDefault(item => ItemIdOf(item) == ItemId);
        if (removed is not null)
        {
            items.Remove(removed);
            _state.Answers[List.FieldId] = JsonSerializer.Serialize(items);
            SaveState(_state);
            Message = DisplayHelpers.GenerateSuccessMessage(List.DeleteItemMessage, "delete", Displayable(removed), List.Title);
        }

        return RedirectToPage(new { task = Task.TaskId });
    }

    public async Task<IActionResult> OnPostCompleteAsync()
    {
        if (!LoadState())
            return Page();
        if (Task is null)
            return RedirectToPage();

        if (IsTaskCompleted)
        {
            CompletionProblems = await draftFormPreview.CompletionProblemsAsync(Template!, Task, _state!.Answers);
            if (CompletionProblems.Count > 0)
            {
                await ShowSummaryAsync();
                return Page();
            }

            _state.Completed.Add(Task.TaskId);
            Message = $"{Task.TaskName} is marked as complete";
        }
        else
        {
            _state!.Completed.Remove(Task.TaskId);
        }

        SaveState(_state);
        return RedirectToPage(new { task = (string?)null });
    }

    public IActionResult OnPostRestart()
    {
        if (!LoadState())
            return Page();
        SaveState(new PreviewState { Schema = _state!.Schema });
        Message = "Answers cleared. You're starting again.";
        return RedirectToPage(new { task = (string?)null });
    }

    // ---- Summaries -------------------------------------------------------------------------------------------------

    private async System.Threading.Tasks.Task ShowSummaryAsync()
    {
        var conditionalState = await draftFormPreview.EvaluateAsync(Template!, _state!.Answers, Task!.TaskId, string.Empty);
        Visibility = draftFormPreview.Visibility(Template!, conditionalState, Task.TaskId, string.Empty);
        Data = _state.Answers;
        View = PreviewView.TaskSummary;
    }

    public IReadOnlyList<Dictionary<string, object>> ItemsOf(MultiCollectionFlowConfiguration flow) =>
        FormEngineCollectionItems.Read(Answers, flow.FieldId);

    public string ItemTitle(MultiCollectionFlowConfiguration flow, Dictionary<string, object> item, int index)
    {
        if (!string.IsNullOrWhiteSpace(flow.ItemTitleBinding))
        {
            var title = DisplayHelpers.InterpolateMessage("{" + flow.ItemTitleBinding + "}", Displayable(item));
            if (!string.IsNullOrWhiteSpace(title) && !title.Contains('{'))
                return title;
        }

        return $"{(string.IsNullOrWhiteSpace(flow.ItemKind) ? "Item" : Capitalise(flow.ItemKind))} {index + 1}";
    }

    public IEnumerable<(string Label, IReadOnlyList<string> Lines)> ItemRows(MultiCollectionFlowConfiguration flow, Dictionary<string, object> item)
    {
        var fields = (flow.Pages ?? []).SelectMany(page => page.Fields ?? []).ToList();
        if (flow.SummaryColumns is { Count: > 0 })
        {
            foreach (var column in flow.SummaryColumns)
            {
                var field = fields.FirstOrDefault(candidate => candidate.FieldId == column.Field);
                item.TryGetValue(column.Field, out var value);
                yield return (column.Label, field is null ? Lines(value?.ToString()) : FormatAnswer(field, value));
            }

            yield break;
        }

        foreach (var field in fields.Where(field => item.ContainsKey(field.FieldId)))
            yield return (QuestionText(field), FormatAnswer(field, item[field.FieldId]));
    }

    public IReadOnlyList<DerivedCollectionItem> DerivedItemsOf(DerivedCollectionFlowConfiguration flow) =>
        derivedCollectionFlowService.GenerateItemsFromSourceField(flow.SourceFieldId, Answers, flow);

    public string DerivedStatus(DerivedCollectionFlowConfiguration flow, DerivedCollectionItem item) =>
        derivedCollectionFlowService.GetItemStatuses(flow.FieldId, Answers).TryGetValue(item.Id, out var status) ? status : "Not signed yet";

    public string? FirstVisiblePageId(IReadOnlyList<TemplatePage>? pages, bool isTaskPages) =>
        (pages ?? []).FirstOrDefault(page => !isTaskPages || Visibility?.IsPageHidden(page.PageId) != true)?.PageId;

    // ---- Formatting ------------------------------------------------------------------------------------------------

    public static string QuestionText(Field field) =>
        string.IsNullOrWhiteSpace(field.Label?.Value) ? field.FieldId : field.Label.Value;

    public IReadOnlyList<string> FormatAnswer(Field field, object? value)
    {
        var values = CheckboxValueNormalizer.Normalize(value).Where(text => !string.IsNullOrWhiteSpace(text)).ToList();
        if (values.Count == 0)
            return [];

        if (field.Type is "radios" or "select" or "checkboxes")
            return values.Select(text => field.Options?.FirstOrDefault(option => option.Value == text)?.Label ?? text).ToList();

        if (field.Type == "date" && DateTime.TryParseExact(values[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return [date.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("en-GB"))];

        if (field.Type == "complexField")
            return SampleNames(values[0]);

        return Lines(DisplayHelpers.UnsanitiseHtmlInput(values[0]));
    }

    private static IReadOnlyList<string> Lines(string? text) =>
        string.IsNullOrWhiteSpace(text) ? [] : text.Split('\n').Select(line => line.TrimEnd('\r')).ToList();

    // ---- Search and upload questions -------------------------------------------------------------------------------

    public bool IsUpload(Field field) =>
        field.ComplexField is not null
        && complexFieldConfigurationService.GetConfiguration(field.ComplexField.Id).FieldType.Equals("upload", StringComparison.OrdinalIgnoreCase);

    public bool AllowsMultiple(Field field) =>
        field.ComplexField is not null && (IsUpload(field) || complexFieldConfigurationService.GetConfiguration(field.ComplexField.Id).AllowMultiple);

    /// <summary>
    /// The sample text shown in the box for a search or upload answer: one name per line.
    /// </summary>
    public string SampleText(Field field) =>
        Data.TryGetValue(field.FieldId, out var value) ? string.Join("\n", SampleNames(value?.ToString())) : string.Empty;

    // Search answers are stored as the selected result, or a list of them, so lists built from them work as they do live.
    private string SampleAnswerJson(Field field, string? text)
    {
        var names = (text ?? string.Empty).Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
        if (names.Count == 0)
            return string.Empty;

        var results = names.Select(name => new Dictionary<string, object> { ["id"] = Slug(name), ["name"] = name }).ToList();
        return AllowsMultiple(field) ? JsonSerializer.Serialize(results) : JsonSerializer.Serialize(results[0]);
    }

    private static IReadOnlyList<string> SampleNames(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            using var document = JsonDocument.Parse(DisplayHelpers.UnsanitiseHtmlInput(json));
            var elements = document.RootElement.ValueKind == JsonValueKind.Array
                ? document.RootElement.EnumerateArray().ToList()
                : [document.RootElement];
            return elements.Select(NameOf).Where(name => name.Length > 0).ToList();
        }
        catch (JsonException)
        {
            return [json];
        }
    }

    private static string NameOf(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return element.ToString();
        foreach (var key in new[] { "name", "displayName", "title", "fileName", "originalFileName" })
        {
            if (element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString() ?? string.Empty;
        }

        return element.EnumerateObject().Select(property => property.Value.ToString()).FirstOrDefault() ?? string.Empty;
    }

    // ---- Pages -----------------------------------------------------------------------------------------------------

    public IReadOnlyList<TemplatePage> PageSet() =>
        List?.Pages ?? Derived?.Pages ?? Task?.Pages ?? [];

    public string? BackUrl()
    {
        var pages = PageSet();
        var index = FormStepPolicy.IndexOfPage(pages, CurrentPage?.PageId ?? string.Empty);
        for (var i = index - 1; index > 0 && i >= 0; i--)
        {
            if (Visibility?.IsPageHidden(pages[i].PageId) == true && List is null && Derived is null)
                continue;
            return Url.Page("/Admin/TemplateStudioPreview", new { task = Task!.TaskId, pageId = pages[i].PageId, list = ListId, derived = DerivedId, item = ItemId, @new = IsNewItem ? true : (bool?)null });
        }

        return Url.Page("/Admin/TemplateStudioPreview", new { task = Task!.TaskId });
    }

    private bool ResolvePage()
    {
        if (!string.IsNullOrEmpty(ListId))
        {
            List = Task!.Summary?.Flows?.FirstOrDefault(flow => flow.FlowId == ListId);
            if (List is null || string.IsNullOrEmpty(ItemId))
                return false;
        }
        else if (!string.IsNullOrEmpty(DerivedId))
        {
            Derived = Task!.Summary?.DerivedFlows?.FirstOrDefault(flow => flow.FlowId == DerivedId);
            if (Derived is null || string.IsNullOrEmpty(ItemId))
                return false;
            ItemName = FormEngineDisplayName(Derived, ItemId);
        }

        CurrentPage = PageSet().FirstOrDefault(page => page.PageId == PageId);
        if (CurrentPage is null)
            return false;

        if (Derived is not null && ItemName is not null)
        {
            CurrentPage.Title = ReplaceDisplayName(CurrentPage.Title, ItemName);
            CurrentPage.Description = ReplaceDisplayName(CurrentPage.Description, ItemName);
        }

        return true;
    }

    // The answers a page shows and its rules see: the application's, then the item being added or changed on top.
    private Dictionary<string, object> DataForPage()
    {
        var data = new Dictionary<string, object>(_state!.Answers);
        if (List is not null)
        {
            var item = FormEngineCollectionItems.Read(_state.Answers, List.FieldId).FirstOrDefault(entry => ItemIdOf(entry) == ItemId);
            foreach (var (key, value) in item ?? [])
                data[key] = Plain(value);
        }
        else if (Derived is not null)
        {
            var source = derivedCollectionFlowService.GenerateItemsFromSourceField(Derived.SourceFieldId, _state.Answers, Derived)
                .FirstOrDefault(entry => entry.Id == ItemId);
            foreach (var (key, value) in source?.PrefilledData ?? [])
                data.TryAdd(key, Plain(value));
            foreach (var (key, value) in derivedCollectionFlowService.GetItemDeclarationData(Derived.FieldId, ItemId!, _state.Answers))
                data[key] = Plain(value);
        }

        return data;
    }

    private IActionResult SaveListItem(DraftPageOutcome outcome)
    {
        var items = FormEngineCollectionItems.Read(_state!.Answers, List!.FieldId);
        var item = items.FirstOrDefault(entry => ItemIdOf(entry) == ItemId);
        if (item is null)
        {
            item = new Dictionary<string, object> { ["id"] = ItemId! };
            items.Add(item);
        }

        foreach (var field in CurrentPage!.Fields ?? [])
        {
            if (outcome.Data.TryGetValue(field.FieldId, out var value))
                item[field.FieldId] = value;
        }

        _state.Answers[List.FieldId] = JsonSerializer.Serialize(items);
        SaveState(_state);

        if (outcome.NextPageId is not null)
            return RedirectToPage(new { task = Task!.TaskId, pageId = outcome.NextPageId, list = List.FlowId, item = ItemId, @new = IsNewItem ? true : (bool?)null });

        Message = DisplayHelpers.GenerateSuccessMessage(
            IsNewItem ? List.AddItemMessage : List.UpdateItemMessage, IsNewItem ? "add" : "update", Displayable(item), List.Title);
        return RedirectToPage(new { task = Task!.TaskId });
    }

    private IActionResult SaveDerivedItem(DraftPageOutcome outcome)
    {
        var declaration = derivedCollectionFlowService.GetItemDeclarationData(Derived!.FieldId, ItemId!, _state!.Answers);
        foreach (var field in CurrentPage!.Fields ?? [])
        {
            if (outcome.Data.TryGetValue(field.FieldId, out var value))
                declaration[field.FieldId] = value;
        }

        var isLast = outcome.NextPageId is null;
        derivedCollectionFlowService.SaveItemDeclaration(Derived.FieldId, ItemId!, declaration, isLast ? "Signed" : "Not signed yet", _state.Answers);
        SaveState(_state);

        if (!isLast)
            return RedirectToPage(new { task = Task!.TaskId, pageId = outcome.NextPageId, derived = Derived.FlowId, item = ItemId });

        var name = FormEngineDisplayName(Derived, ItemId!);
        Message = (Derived.SignedMessage ?? "Declaration for {displayName} has been signed")
            .Replace("{displayName}", name)
            .Replace("{name}", name);
        return RedirectToPage(new { task = Task!.TaskId });
    }

    private string FormEngineDisplayName(DerivedCollectionFlowConfiguration flow, string itemId)
    {
        var item = derivedCollectionFlowService.GenerateItemsFromSourceField(flow.SourceFieldId, _state!.Answers, flow)
            .FirstOrDefault(entry => string.Equals(entry.Id, itemId, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(item?.DisplayName) ? itemId : item.DisplayName;
    }

    private string? FirstPageWhenNotStarted()
    {
        if (Task!.StartAtFirstPageWhenNotStarted != true || IsCompleted(Task.TaskId) || HasAnswers(Task))
            return null;
        return Task.Pages?.FirstOrDefault()?.PageId;
    }

    public bool HasAnswers(TaskModel task)
    {
        var ids = (task.Pages ?? []).SelectMany(page => page.Fields ?? []).Select(field => field.FieldId)
            .Concat((task.Summary?.Flows ?? []).Select(flow => flow.FieldId))
            .Concat((task.Summary?.DerivedFlows ?? []).Select(flow => flow.FieldId + "_status_"));
        return ids.Any(id => Answers.Any(answer =>
            (answer.Key == id || (id.EndsWith("_status_") && answer.Key.StartsWith(id)))
            && !string.IsNullOrWhiteSpace(answer.Value?.ToString())));
    }

    private static string ReplaceDisplayName(string? text, string name) =>
        (text ?? string.Empty).Replace("{displayName}", name).Replace("{name}", name);

    // ---- State -----------------------------------------------------------------------------------------------------

    public sealed class PreviewState
    {
        public string Schema { get; set; } = string.Empty;
        public Dictionary<string, object> Answers { get; set; } = [];
        public HashSet<string> Completed { get; set; } = [];
    }

    private bool LoadState()
    {
        var json = HttpContext.Session.GetString(SessionKey);
        if (string.IsNullOrEmpty(json))
            return false;

        _state = JsonSerializer.Deserialize<PreviewState>(json);
        if (_state is null)
            return false;
        if (!TryParse(_state.Schema, out var template, out var error))
        {
            ParseError = error;
            return false;
        }

        _state.Answers = _state.Answers.ToDictionary(pair => pair.Key, pair => Plain(pair.Value));
        Template = template;
        Problems = templateValidationService.ValidateTemplateJson(_state.Schema).Errors;
        RuleTargets = (template.ConditionalLogic ?? [])
            .Where(rule => rule.Enabled)
            .SelectMany(rule => rule.AffectedElements ?? [])
            .Select(element => element.ElementId)
            .ToHashSet(StringComparer.Ordinal);
        Task = string.IsNullOrEmpty(TaskId) ? null : AllTasks(template).FirstOrDefault(task => task.TaskId == TaskId);
        PageSets = Task is null ? [] : BuildPageSets(Task);
        return true;
    }

    private void SaveState(PreviewState state) =>
        HttpContext.Session.SetString(SessionKey, JsonSerializer.Serialize(state));

    private static bool TryParse(string schema, out FormTemplate template, out string? error)
    {
        template = null!;
        error = null;
        try
        {
            template = JsonSerializer.Deserialize<FormTemplate>(schema, ParseOptions)!;
        }
        catch (JsonException ex)
        {
            error = $"This template can't be previewed: {ex.Message}";
            return false;
        }

        if (template is null)
        {
            error = "This template can't be previewed because it's empty.";
            return false;
        }

        return true;
    }

    // Answers come back from the session as JSON; the form engine expects strings and string arrays.
    private static object Plain(object value) => value switch
    {
        JsonElement { ValueKind: JsonValueKind.String } text => text.GetString() ?? string.Empty,
        JsonElement { ValueKind: JsonValueKind.Array } array when array.EnumerateArray().All(entry => entry.ValueKind == JsonValueKind.String)
            => array.EnumerateArray().Select(entry => entry.GetString() ?? string.Empty).ToArray(),
        JsonElement element => element.ToString(),
        _ => value
    };

    private static Dictionary<string, object> Displayable(Dictionary<string, object> item) =>
        DisplayHelpers.ExpandEncodedJson(item.ToDictionary(pair => pair.Key, pair => pair.Value is string text ? JsonSerializer.SerializeToElement(text) : pair.Value)) ?? item;

    private static string ItemIdOf(Dictionary<string, object> item) =>
        item.TryGetValue("id", out var id) ? id?.ToString() ?? string.Empty : string.Empty;

    public static IEnumerable<TaskModel> AllTasks(FormTemplate template) =>
        (template.TaskGroups ?? []).SelectMany(group => group.Tasks ?? []);

    private static List<PreviewPageSet> BuildPageSets(TaskModel task)
    {
        var sets = new List<PreviewPageSet>();
        if (task.Pages is { Count: > 0 })
            sets.Add(new PreviewPageSet(null, task.Pages));

        foreach (var flow in task.Summary?.Flows ?? [])
            sets.Add(new PreviewPageSet($"Repeatable list: {flow.Title}", flow.Pages ?? []));

        foreach (var flow in task.Summary?.DerivedFlows ?? [])
            sets.Add(new PreviewPageSet($"Repeated pages: {flow.Title}", flow.Pages ?? []));

        return sets;
    }

    private static string Capitalise(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static string Slug(string text) => SlugRegex().Replace(text.ToLowerInvariant(), "-").Trim('-');

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex SlugRegex();
}

public sealed record PreviewPageSet(string? Title, IReadOnlyList<TemplatePage> Pages);
