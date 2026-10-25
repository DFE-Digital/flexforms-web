using System.Text.Json;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Templates;
using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Visual editor for the template selected in Template Manager. The browser edits the JSON in memory and posts it
/// back as a new version through the same validation and save path as the JSON textarea.
/// </summary>
[Authorize(Policy = AdminAccessHelper.CanManageTemplatesPolicy)]
[RequestSizeLimit(52_428_800)]
[RequestFormLimits(ValueLengthLimit = 52_428_800, ValueCountLimit = 1000)]
public class TemplateEditorModel(
    ITemplateManagerAdmin templateManagerAdmin,
    ITemplateSelectionService templateSelectionService,
    ITemplateValidationService templateValidationService,
    ITemplateCacheInvalidator templateCacheInvalidator,
    IComplexFieldConfigurationService complexFieldConfigurationService,
    IApplicationsClient applicationsClient,
    ILogger<TemplateEditorModel> logger) : PageModel
{
    private const string TemplateVersionSessionKey = "TemplateVersionNumber";

    public TemplateDto? SelectedTemplate { get; private set; }
    public string? CurrentVersionNumber { get; private set; }
    public string? LatestVersionNumber { get; private set; }
    public string? SavedVersion { get; private set; }
    public string? LoadError { get; private set; }
    public string EditorDataJson { get; private set; } = "{}";

    [BindProperty]
    public string? NewVersion { get; set; }

    [BindProperty]
    public string? NewSchema { get; set; }

    [BindProperty]
    public bool AcknowledgeReportingImpact { get; set; }

    public async Task<IActionResult> OnGetAsync(string? saved, CancellationToken cancellationToken)
    {
        var state = await LoadSelectedTemplateAsync(cancellationToken);
        if (state is null)
            return RedirectToPage("/Admin/TemplateManager");

        SavedVersion = saved;
        if (LoadError is not null)
            return Page();

        NewVersion = SuggestVersion(state);
        await BuildEditorDataAsync(state, schemaJson: null, hasUnsavedChanges: false, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        var state = await LoadSelectedTemplateAsync(cancellationToken);
        if (state is null)
            return RedirectToPage("/Admin/TemplateManager");

        // The posted JSON is the designer's work, so keep the editor open with it even if reloading the stored version failed.
        if (!string.IsNullOrWhiteSpace(NewSchema))
            LoadError = null;

        state.NewVersion = NewVersion?.Trim();
        state.NewSchema = NewSchema;
        state.AcknowledgeReportingImpact = AcknowledgeReportingImpact;

        var outcome = templateManagerAdmin.ValidateNewVersion(state);
        if (outcome.Errors.Count == 0)
            outcome = await templateManagerAdmin.CreateVersionAsync(state, SelectedTemplate!.TemplateId, cancellationToken);

        if (outcome.Errors.Count > 0)
        {
            foreach (var error in outcome.Errors)
                ModelState.AddModelError(error.FieldKey, error.Message);

            await BuildEditorDataAsync(state, schemaJson: NewSchema, hasUnsavedChanges: true, cancellationToken);
            return Page();
        }

        logger.LogInformation("Template editor saved version {Version} for {TemplateId}",
            state.NewVersion, SelectedTemplate!.TemplateId);

        await templateCacheInvalidator.InvalidateAsync(SelectedTemplate.TemplateId.ToString());
        HttpContext.Session.SetString(TemplateVersionSessionKey, state.NewVersion!);
        await HttpContext.Session.CommitAsync(cancellationToken);

        return RedirectToPage(new { saved = state.NewVersion });
    }

    /// <summary>
    /// Runs the save-time checks without saving, so the editor can list problems as the designer works.
    /// </summary>
    public IActionResult OnPostValidate([FromBody] TemplateEditorValidateRequest? request)
    {
        var (isValid, errors) = templateValidationService.ValidateTemplateJson(request?.Schema ?? string.Empty);
        return new JsonResult(new { isValid, errors });
    }

    private async Task<TemplateManagerWorkState?> LoadSelectedTemplateAsync(CancellationToken cancellationToken)
    {
        var templates = await templateSelectionService.GetSelectableTemplatesAsync(cancellationToken);
        var sessionTemplateId = templateSelectionService.GetSelectedTemplateId(HttpContext);
        if (!Guid.TryParse(sessionTemplateId, out var templateId)
            || templates.FirstOrDefault(template => template.TemplateId == templateId) is not { } template)
        {
            return null;
        }

        SelectedTemplate = template;

        var state = new TemplateManagerWorkState
        {
            TenantTemplates = templates,
            SessionVersionNumber = HttpContext.Session.GetString(TemplateVersionSessionKey)
        };
        await templateManagerAdmin.LoadTemplateDataAsync(state, templateId, cancellationToken);

        if (state.HasError)
            LoadError = state.ErrorMessage;

        CurrentVersionNumber = state.CurrentVersionNumber;
        LatestVersionNumber = state.LatestVersionNumber;
        return state;
    }

    private string SuggestVersion(TemplateManagerWorkState state) =>
        state.AvailableVersions.Count == 0
            ? StarterFormTemplateSchema.DefaultVersionNumber
            : templateManagerAdmin.SuggestNextVersion(state.LatestVersionNumber, state.CurrentVersionNumber);

    /// <summary>
    /// How many applications use the template, or null when that can't be checked. Once there are any, the API
    /// rejects versions that drop or change questions without retiring them, so the editor retires them instead.
    /// </summary>
    private async Task<int?> CountApplicationsAsync(Guid templateId, CancellationToken cancellationToken)
    {
        try
        {
            var page = await applicationsClient.GetApplicationsByTemplateAsync(
                templateId,
                includeSchema: false,
                pageNumber: 1,
                pageSize: 1,
                cancellationToken: cancellationToken);
            return page.TotalCount;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not count applications for template {TemplateId}", templateId);
            return null;
        }
    }

    private async Task BuildEditorDataAsync(
        TemplateManagerWorkState state,
        string? schemaJson,
        bool hasUnsavedChanges,
        CancellationToken cancellationToken)
    {
        var isStarter = false;
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            schemaJson = state.CurrentTemplateJson;
        }

        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            schemaJson = StarterFormTemplateSchema.CreateJson(SelectedTemplate!.TemplateId.ToString(), SelectedTemplate.Name);
            isStarter = true;
        }

        // Only ids, kinds and labels: the configuration also holds API keys and client secrets.
        var complexFields = complexFieldConfigurationService.GetAllConfigurations()
            .Select(configuration => new
            {
                id = configuration.Id,
                fieldType = configuration.FieldType,
                label = configuration.Label
            });

        var applicationCount = state.AvailableVersions.Count == 0
            ? 0
            : await CountApplicationsAsync(SelectedTemplate!.TemplateId, cancellationToken);

        EditorDataJson = JsonSerializer.Serialize(new
        {
            templateName = SelectedTemplate!.Name,
            versionNumber = CurrentVersionNumber,
            schemaJson,
            // The stored version is what reporting compares a new version with.
            baselineSchemaJson = state.CurrentTemplateJson,
            applicationCount,
            isStarter,
            hasUnsavedChanges,
            complexFields,
            validateUrl = Url.Page("/Admin/TemplateEditor", "Validate")
        });
    }
}

public sealed record TemplateEditorValidateRequest(string? Schema);
