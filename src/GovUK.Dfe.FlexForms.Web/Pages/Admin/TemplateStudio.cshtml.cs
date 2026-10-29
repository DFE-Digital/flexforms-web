using System.Text.Json;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Templates;
using GovUK.Dfe.FlexForms.Web.Features;
using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.FeatureManagement.Mvc;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Template Studio: visual editing for the template selected in Template Manager. The browser edits the JSON in memory and posts it
/// back as a new version through the same validation and save path as the JSON textarea.
/// </summary>
[Authorize(Policy = AdminAccessHelper.CanManageTemplatesPolicy)]
[FeatureGate(FeatureFlags.TemplateStudio)]
[RequestSizeLimit(TemplateStudioRequestLimits.MaxBytes)]
[RequestFormLimits(ValueLengthLimit = TemplateStudioRequestLimits.MaxBytes, ValueCountLimit = 1000)]
public class TemplateStudioModel(
    ITemplateManagerAdmin templateManagerAdmin,
    ITemplateSelectionService templateSelectionService,
    ITemplateValidationService templateValidationService,
    ITemplateCacheInvalidator templateCacheInvalidator,
    IComplexFieldConfigurationService complexFieldConfigurationService,
    IEventMappingReferenceReader eventMappingReferenceReader,
    IApplicationsClient applicationsClient,
    ILogger<TemplateStudioModel> logger) : PageModel
{
    private const string TemplateVersionSessionKey = "TemplateVersionNumber";

    public TemplateDto? SelectedTemplate { get; private set; }
    public IReadOnlyList<TemplateDto> TenantTemplates { get; private set; } = [];
    public IReadOnlyList<TemplateVersionSummaryDto> AvailableVersions { get; private set; } = [];
    public string? CurrentVersionNumber { get; private set; }
    public string? LatestVersionNumber { get; private set; }
    public string? SavedVersion { get; private set; }
    public bool ShowCreated { get; private set; }
    public string? GrantToAllUsersSummary { get; private set; }

    [BindProperty]
    public Guid? SelectedTemplateId { get; set; }

    [BindProperty]
    public string? SelectedVersionNumber { get; set; }
    public string? LoadError { get; private set; }
    public string StudioDataJson { get; private set; } = "{}";

    [BindProperty]
    public string? NewVersion { get; set; }

    [BindProperty]
    public string? NewSchema { get; set; }

    [BindProperty]
    public bool AcknowledgeReportingImpact { get; set; }

    /// <summary>
    /// The latest version when Template Studio was opened. A different latest version at save time means someone
    /// else saved in between, and saving would leave their changes out of the new version.
    /// </summary>
    [BindProperty]
    public string? StartedFromLatestVersion { get; set; }

    [BindProperty]
    public bool SaveOverNewerVersion { get; set; }

    public bool HasNewerVersion { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        string? saved,
        bool created,
        CancellationToken cancellationToken,
        Guid? templateId = null,
        string? version = null)
    {
        if (TempData?["TemplateStudioGrantSummary"] is string grantSummary)
            GrantToAllUsersSummary = grantSummary;

        var state = await LoadSelectedTemplateAsync(
            cancellationToken,
            selectFirstWhenNone: true,
            requestedTemplateId: templateId,
            requestedVersion: version);
        if (state is null)
            return Page();

        SavedVersion = saved;
        ShowCreated = created;
        if (LoadError is not null)
            return Page();

        StartedFromLatestVersion = LatestVersionNumber;
        NewVersion = SuggestVersion(state);
        await BuildStudioDataAsync(state, schemaJson: null, hasUnsavedChanges: false, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        NewSchema = TemplateStudioSchemaTransport.Decode(NewSchema);
        // The posted template id is this tab's template. Session may belong to another tab.
        var state = await LoadSelectedTemplateAsync(
            cancellationToken,
            selectFirstWhenNone: false,
            requestedTemplateId: SelectedTemplateId,
            requestedVersion: SelectedVersionNumber);
        if (state is null)
            return RedirectToPage();

        // The posted JSON is the designer's work, so keep Template Studio open with it even if reloading the stored version failed.
        if (!string.IsNullOrWhiteSpace(NewSchema))
            LoadError = null;

        state.NewVersion = NewVersion?.Trim();
        state.NewSchema = NewSchema;
        state.AcknowledgeReportingImpact = AcknowledgeReportingImpact;

        HasNewerVersion = !string.Equals(StartedFromLatestVersion ?? string.Empty, LatestVersionNumber ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        if (HasNewerVersion && !SaveOverNewerVersion)
        {
            ModelState.AddModelError(nameof(SaveOverNewerVersion),
                $"Version {LatestVersionNumber} was saved after you opened Template Studio. Your version won't include its changes. Check version {LatestVersionNumber} in Template Manager, or confirm you want to save anyway");
        }

        var outcome = templateManagerAdmin.ValidateNewVersion(state);
        if (outcome.Errors.Count == 0 && ModelState.IsValid)
            outcome = await templateManagerAdmin.CreateVersionAsync(state, SelectedTemplate!.TemplateId, cancellationToken);

        if (outcome.Errors.Count > 0 || !ModelState.IsValid)
        {
            foreach (var error in outcome.Errors)
                ModelState.AddModelError(error.FieldKey, error.Message);

            await BuildStudioDataAsync(state, schemaJson: NewSchema, hasUnsavedChanges: true, cancellationToken);
            return Page();
        }

        logger.LogInformation("Template Studio saved version {Version} for {TemplateId}",
            state.NewVersion, SelectedTemplate!.TemplateId);

        await templateCacheInvalidator.InvalidateAsync(SelectedTemplate.TemplateId.ToString());
        await templateSelectionService.SelectTemplateAsync(HttpContext, SelectedTemplate, cancellationToken);
        HttpContext.Session.SetString(TemplateVersionSessionKey, state.NewVersion!);
        await HttpContext.Session.CommitAsync(cancellationToken);
        CurrentVersionNumber = state.NewVersion;

        return RedirectToStudio(saved: state.NewVersion);
    }

    /// <summary>
    /// Grants read and write access for the template open in this tab to every active user in the tenant.
    /// </summary>
    public async Task<IActionResult> OnPostGrantToAllUsersAsync(CancellationToken cancellationToken)
    {
        var state = await LoadSelectedTemplateAsync(
            cancellationToken,
            selectFirstWhenNone: false,
            requestedTemplateId: SelectedTemplateId,
            requestedVersion: SelectedVersionNumber);
        if (state is null || SelectedTemplate is null)
        {
            ModelState.AddModelError(string.Empty, TemplateManagerMessages.GrantRequiresTemplate);
            return Page();
        }

        var outcome = await templateManagerAdmin.GrantToAllUsersAsync(state, SelectedTemplate.TemplateId, cancellationToken);
        if (outcome.Kind == AdminPageOutcomeKind.StayOnPage)
        {
            ModelState.AddModelError(string.Empty, outcome.ErrorMessage ?? TemplateManagerMessages.GrantFailed);
            StartedFromLatestVersion = LatestVersionNumber;
            NewVersion = SuggestVersion(state);
            await BuildStudioDataAsync(state, schemaJson: null, hasUnsavedChanges: false, cancellationToken);
            return Page();
        }

        TempData["TemplateStudioGrantSummary"] = state.GrantToAllUsersSummary ?? outcome.SuccessMessage;
        await templateSelectionService.SelectTemplateAsync(HttpContext, SelectedTemplate, cancellationToken);
        return RedirectToStudio();
    }

    /// <summary>
    /// Runs the save-time checks without saving, so Template Studio can list problems as the designer works.
    /// </summary>
    public IActionResult OnPostValidate([FromBody] TemplateStudioValidateRequest? request)
    {
        var schema = TemplateStudioSchemaTransport.Decode(request?.Schema);
        var (isValid, errors) = templateValidationService.ValidateTemplateJson(schema ?? string.Empty);
        return new JsonResult(new { isValid, errors });
    }

    /// <summary>
    /// Opens another template. Any unsaved changes are lost, which the browser warns about before the form submits.
    /// </summary>
    public async Task<IActionResult> OnPostSelectTemplateAsync(CancellationToken cancellationToken)
    {
        var templates = await templateSelectionService.GetSelectableTemplatesAsync(cancellationToken);
        if (templates.FirstOrDefault(template => template.TemplateId == SelectedTemplateId) is { } template)
        {
            await templateSelectionService.SelectTemplateAsync(HttpContext, template, cancellationToken);
            HttpContext.Session.Remove(TemplateVersionSessionKey);
            await HttpContext.Session.CommitAsync(cancellationToken);
            SelectedTemplate = template;
            SelectedTemplateId = template.TemplateId;
            CurrentVersionNumber = null;
        }

        return RedirectToStudio();
    }

    /// <summary>
    /// Opens another version of the current template as the starting point for the next version.
    /// </summary>
    public async Task<IActionResult> OnPostSelectVersionAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(SelectedVersionNumber))
        {
            HttpContext.Session.SetString(TemplateVersionSessionKey, SelectedVersionNumber.Trim());
            await HttpContext.Session.CommitAsync(cancellationToken);
            CurrentVersionNumber = SelectedVersionNumber.Trim();
        }

        return RedirectToStudio();
    }

    private RedirectToPageResult RedirectToStudio(string? saved = null)
    {
        var templateId = SelectedTemplate?.TemplateId ?? SelectedTemplateId;
        return RedirectToPage(new
        {
            saved,
            templateId,
            version = CurrentVersionNumber
        });
    }

    private async Task<TemplateManagerWorkState?> LoadSelectedTemplateAsync(
        CancellationToken cancellationToken,
        bool selectFirstWhenNone,
        Guid? requestedTemplateId = null,
        string? requestedVersion = null)
    {
        TenantTemplates = await templateSelectionService.GetSelectableTemplatesAsync(cancellationToken);
        TemplateDto? template;
        if (requestedTemplateId is Guid requested)
        {
            template = TenantTemplates.FirstOrDefault(item => item.TemplateId == requested);
        }
        else
        {
            var sessionTemplateId = templateSelectionService.GetSelectedTemplateId(HttpContext);
            template = Guid.TryParse(sessionTemplateId, out var sessionId)
                ? TenantTemplates.FirstOrDefault(item => item.TemplateId == sessionId)
                : null;
        }

        if (template is null && selectFirstWhenNone && TenantTemplates.FirstOrDefault() is { } first)
        {
            await templateSelectionService.SelectTemplateAsync(HttpContext, first, cancellationToken);
            HttpContext.Session.Remove(TemplateVersionSessionKey);
            template = first;
        }

        if (template is null)
            return null;

        SelectedTemplate = template;
        SelectedTemplateId = template.TemplateId;

        var state = new TemplateManagerWorkState
        {
            TenantTemplates = TenantTemplates,
            SessionVersionNumber = !string.IsNullOrWhiteSpace(requestedVersion)
                ? requestedVersion.Trim()
                : requestedTemplateId is null
                    ? HttpContext.Session.GetString(TemplateVersionSessionKey)
                    : null
        };
        await templateManagerAdmin.LoadTemplateDataAsync(state, template.TemplateId, cancellationToken);

        if (state.HasError)
            LoadError = state.ErrorMessage;

        AvailableVersions = state.AvailableVersions;
        CurrentVersionNumber = state.CurrentVersionNumber;
        LatestVersionNumber = state.LatestVersionNumber;
        SelectedVersionNumber = state.CurrentVersionNumber;
        return state;
    }

    private string SuggestVersion(TemplateManagerWorkState state) =>
        state.AvailableVersions.Count == 0
            ? StarterFormTemplateSchema.DefaultVersionNumber
            : templateManagerAdmin.SuggestNextVersion(state.LatestVersionNumber, state.CurrentVersionNumber);

    /// <summary>
    /// How many applications use the template, or null when that can't be checked. Once there are any, the API
    /// rejects versions that drop or change questions without retiring them, so Template Studio retires them instead.
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

    private async Task BuildStudioDataAsync(
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

        // Only ids, kinds, labels and display layouts: the configuration also holds API keys and client secrets.
        var complexFields = complexFieldConfigurationService.GetAllConfigurations()
            .Select(configuration => new
            {
                id = configuration.Id,
                fieldType = configuration.FieldType,
                label = configuration.Label,
                dropdownDisplay = configuration.DropdownDisplay,
                confirmationDisplay = configuration.ConfirmationDisplay,
                summaryDisplay = configuration.SummaryDisplay
            });

        var applicationCount = state.AvailableVersions.Count == 0
            ? 0
            : await CountApplicationsAsync(SelectedTemplate!.TemplateId, cancellationToken);

        StudioDataJson = JsonSerializer.Serialize(new
        {
            templateId = SelectedTemplate!.TemplateId,
            templateName = SelectedTemplate.Name,
            versionNumber = CurrentVersionNumber,
            schemaJson,
            // The stored version is what reporting compares a new version with.
            baselineSchemaJson = state.CurrentTemplateJson,
            applicationCount,
            isStarter,
            hasUnsavedChanges,
            complexFields,
            eventMappings = ReadEventMappingReferences(schemaJson, state.CurrentTemplateJson)
                .Select(reference => new { fieldId = reference.FieldId, eventType = reference.EventType, mappingId = reference.MappingId }),
            validateUrl = Url.Page("/Admin/TemplateStudio", "Validate")
        });
    }

    private IReadOnlyList<EventMappingFieldReference> ReadEventMappingReferences(params string?[] schemas)
    {
        var keys = new List<string> { SelectedTemplate!.TemplateId.ToString() };
        keys.AddRange(schemas.Select(EmbeddedTemplateId).OfType<string>());
        try
        {
            return eventMappingReferenceReader.GetFieldReferences(keys);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read event mappings for template {TemplateId}", SelectedTemplate.TemplateId);
            return [];
        }
    }

    private static string? EmbeddedTemplateId(string? schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
            return null;
        try
        {
            using var document = JsonDocument.Parse(schemaJson);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("templateId", out var id)
                && id.ValueKind == JsonValueKind.String
                ? id.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public sealed record TemplateStudioValidateRequest(string? Schema);
