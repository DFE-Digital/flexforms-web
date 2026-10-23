using System.Text.Json;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TaskModel = GovUK.Dfe.FlexForms.Domain.Models.Task;
using TemplatePage = GovUK.Dfe.FlexForms.Domain.Models.Page;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Shows one task of an unsaved template as applicants would see it, without creating an application.
/// </summary>
[Authorize(Policy = AdminAccessHelper.CanManageTemplatesPolicy)]
[RequestSizeLimit(52_428_800)]
[RequestFormLimits(ValueLengthLimit = 52_428_800, ValueCountLimit = 1000)]
public class TemplateEditorPreviewModel(ITemplateValidationService templateValidationService) : PageModel
{
    // Matches how the form engine reads templates (JsonFormTemplateParser).
    private static readonly JsonSerializerOptions ParseOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    [BindProperty]
    public string? Schema { get; set; }

    [BindProperty]
    public string? TaskId { get; set; }

    public FormTemplate? Template { get; private set; }
    public TaskModel? Task { get; private set; }
    public IReadOnlyList<PreviewPageSet> PageSets { get; private set; } = [];
    public IReadOnlyList<string> Problems { get; private set; } = [];
    public IReadOnlySet<string> RuleTargets { get; private set; } = new HashSet<string>();
    public string? ParseError { get; private set; }

    public IActionResult OnGet() => RedirectToPage("/Admin/TemplateEditor");

    public IActionResult OnPost()
    {
        if (string.IsNullOrWhiteSpace(Schema))
        {
            ParseError = "There's no template to preview. Go back to the editor and try again.";
            return Page();
        }

        try
        {
            Template = JsonSerializer.Deserialize<FormTemplate>(Schema, ParseOptions);
        }
        catch (JsonException ex)
        {
            ParseError = $"This template can't be previewed: {ex.Message}";
            return Page();
        }

        if (Template is null)
        {
            ParseError = "This template can't be previewed because it's empty.";
            return Page();
        }

        Problems = templateValidationService.ValidateTemplateJson(Schema).Errors;

        var tasks = (Template.TaskGroups ?? []).SelectMany(group => group.Tasks ?? []).ToList();
        Task = tasks.FirstOrDefault(task => task.TaskId == TaskId) ?? tasks.FirstOrDefault();
        TaskId = Task?.TaskId;
        PageSets = Task is null ? [] : BuildPageSets(Task);

        RuleTargets = (Template.ConditionalLogic ?? [])
            .Where(rule => rule.Enabled)
            .SelectMany(rule => rule.AffectedElements ?? [])
            .Select(element => element.ElementId)
            .ToHashSet(StringComparer.Ordinal);

        return Page();
    }

    private static List<PreviewPageSet> BuildPageSets(TaskModel task)
    {
        var sets = new List<PreviewPageSet>();
        if (task.Pages is { Count: > 0 })
            sets.Add(new PreviewPageSet(null, task.Pages));

        foreach (var flow in task.Summary?.Flows ?? [])
            sets.Add(new PreviewPageSet($"Repeatable list: {flow.Title}", flow.Pages ?? []));

        foreach (var flow in task.Summary?.DerivedFlows ?? [])
            sets.Add(new PreviewPageSet($"Derived list: {flow.Title}", flow.Pages ?? []));

        return sets;
    }
}

public sealed record PreviewPageSet(string? Title, IReadOnlyList<TemplatePage> Pages);
