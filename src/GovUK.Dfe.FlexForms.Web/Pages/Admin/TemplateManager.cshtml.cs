using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Web.Services;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using Task = System.Threading.Tasks.Task;
using GovUK.Dfe.CoreLibs.Caching.Interfaces;
using GovUK.Dfe.CoreLibs.Caching.Helpers;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

[Authorize(Policy = AdminAccessHelper.CanManageTemplatesPolicy)]
[RequestSizeLimit(52_428_800)]
[RequestFormLimits(ValueLengthLimit = 52_428_800, ValueCountLimit = 1000)]
public class TemplateManagerModel(
    IFormTemplateProvider formTemplateProvider,
    ITemplateManagerAdmin templateManagerAdmin,
    ITemplateSelectionService templateSelectionService,
    ICacheService<IMemoryCacheType> cacheService,
    ILogger<TemplateManagerModel> logger) : PageModel
{
    private const string TemplateVersionSessionKey = "TemplateVersionNumber";

    public FormTemplate? CurrentTemplate { get; set; }
    public string? CurrentVersionNumber { get; set; }
    public string? LatestVersionNumber { get; set; }
    public string? CurrentTemplateJson { get; set; }
    public bool ShowAddVersionForm { get; set; }
    public bool HasError { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public bool ShowSuccess { get; set; }
    public bool ShowCacheCleared { get; set; }
    public bool ShowCreated { get; set; }
    public bool ShowGrantedToAllUsers { get; set; }
    public string? GrantToAllUsersSummary { get; set; }
    public IReadOnlyList<TemplateDto> TenantTemplates { get; private set; } = [];
    public IReadOnlyList<TemplateVersionSummaryDto> AvailableVersions { get; private set; } = [];
    public TemplateDto? SelectedTemplate { get; private set; }

    [BindProperty]
    public Guid? SelectedTemplateId { get; set; }

    [BindProperty]
    public string? SelectedVersionNumber { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Version number is required")]
    public string? NewVersion { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "JSON schema is required")]
    public string? NewSchema { get; set; }

    [BindProperty]
    public bool AcknowledgeReportingImpact { get; set; }

    public async Task<IActionResult> OnGetAsync(
        bool showForm = false,
        bool success = false,
        bool cleared = false,
        bool created = false,
        string? suggestedVersion = null)
    {
        try
        {
            logger.LogInformation("TemplateManager GET started. Memory: {MemoryMB} MB",
                GC.GetTotalMemory(false) / 1024 / 1024);

            ShowAddVersionForm = showForm;
            ShowSuccess = success;
            ShowCacheCleared = cleared;
            ShowCreated = created;

            if (TempData["TemplateManagerGrantSummary"] is string grantSummary)
            {
                ShowGrantedToAllUsers = true;
                GrantToAllUsersSummary = grantSummary;
            }

            await LoadTenantTemplatesAsync();
            var templateId = await ResolveSelectedTemplateIdAsync();
            if (templateId is null)
                return Page();

            var state = CaptureWorkState();
            await templateManagerAdmin.LoadTemplateDataAsync(state, templateId.Value);
            ApplyWorkState(state);
            PersistSessionVersion(state);

            if (!string.IsNullOrEmpty(suggestedVersion))
            {
                NewVersion = suggestedVersion;
                logger.LogInformation("Pre-populated NewVersion field with suggested version: {SuggestedVersion}", suggestedVersion);
            }

            PrefillNewSchema(templateId.Value);

            logger.LogInformation("TemplateManager GET completed successfully. Memory: {MemoryMB} MB",
                GC.GetTotalMemory(false) / 1024 / 1024);

            return Page();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CRITICAL ERROR in TemplateManager OnGetAsync. Memory: {MemoryMB} MB, Exception Type: {ExceptionType}",
                GC.GetTotalMemory(false) / 1024 / 1024, ex.GetType().FullName);
            throw;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadTenantTemplatesAsync();
        var templateId = await ResolveSelectedTemplateIdAsync();
        if (templateId is null)
        {
            ModelState.AddModelError(string.Empty, TemplateManagerMessages.SelectTemplate);
            return Page();
        }

        var state = CaptureWorkState();
        var validation = templateManagerAdmin.ValidateNewVersion(state);
        if (validation.Errors.Count > 0)
            return await RedisplayAddVersionFormAsync(state, templateId.Value, validation);

        var created = await templateManagerAdmin.CreateVersionAsync(state, templateId.Value);
        if (created.Kind == AdminPageOutcomeKind.StayOnPage || created.Errors.Count > 0)
            return await RedisplayAddVersionFormAsync(state, templateId.Value, created);

        await InvalidateTemplateCacheAsync(templateId.Value.ToString());

        HttpContext.Session.SetString(TemplateVersionSessionKey, NewVersion!);
        await HttpContext.Session.CommitAsync();

        return RedirectToPage(new { success = true });
    }

    public async Task<IActionResult> OnPostSelectTemplateAsync(CancellationToken cancellationToken)
    {
        await LoadTenantTemplatesAsync(cancellationToken);

        if (SelectedTemplateId is null ||
            TenantTemplates.All(template => template.TemplateId != SelectedTemplateId.Value))
        {
            ModelState.AddModelError(nameof(SelectedTemplateId), TemplateManagerMessages.SelectTenantTemplate);
            return Page();
        }

        var template = TenantTemplates.First(item => item.TemplateId == SelectedTemplateId.Value);
        await templateSelectionService.SelectTemplateAsync(HttpContext, template, cancellationToken);
        HttpContext.Session.Remove(TemplateVersionSessionKey);
        await HttpContext.Session.CommitAsync(cancellationToken);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSelectVersionAsync(CancellationToken cancellationToken)
    {
        await LoadTenantTemplatesAsync(cancellationToken);
        var templateId = await ResolveSelectedTemplateIdAsync(cancellationToken);
        if (templateId is null)
        {
            ModelState.AddModelError(string.Empty, TemplateManagerMessages.SelectTemplate);
            return Page();
        }

        if (string.IsNullOrWhiteSpace(SelectedVersionNumber))
        {
            ModelState.AddModelError(nameof(SelectedVersionNumber), TemplateManagerMessages.SelectVersion);
            var state = CaptureWorkState();
            await templateManagerAdmin.LoadTemplateDataAsync(state, templateId.Value, cancellationToken);
            ApplyWorkState(state);
            return Page();
        }

        HttpContext.Session.SetString(TemplateVersionSessionKey, SelectedVersionNumber.Trim());
        await HttpContext.Session.CommitAsync(cancellationToken);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostShowAddFormAsync()
    {
        await LoadTenantTemplatesAsync();
        var templateId = await ResolveSelectedTemplateIdAsync();
        if (templateId is not null)
        {
            var state = CaptureWorkState();
            await templateManagerAdmin.LoadTemplateDataAsync(state, templateId.Value);
            ApplyWorkState(state);

            var baseVersion = LatestVersionNumber ?? CurrentVersionNumber;
            if (!string.IsNullOrEmpty(baseVersion))
            {
                var incrementedVersion = templateManagerAdmin.SuggestNextVersion(LatestVersionNumber, CurrentVersionNumber);
                logger.LogInformation(
                    "Auto-incremented version from {LatestVersion} to {NewVersion} (selected schema version {SelectedVersion})",
                    baseVersion, incrementedVersion, CurrentVersionNumber);

                return RedirectToPage(new { showForm = true, suggestedVersion = incrementedVersion });
            }
        }

        return RedirectToPage(new { showForm = true });
    }

    public async Task<IActionResult> OnPostGrantToAllUsersAsync(CancellationToken cancellationToken)
    {
        await LoadTenantTemplatesAsync(cancellationToken);
        var templateId = await ResolveSelectedTemplateIdAsync(cancellationToken);
        if (templateId is null)
        {
            HasError = true;
            ErrorMessage = TemplateManagerMessages.GrantRequiresTemplate;
            return Page();
        }

        var state = CaptureWorkState();
        var outcome = await templateManagerAdmin.GrantToAllUsersAsync(state, templateId.Value, cancellationToken);
        ApplyWorkState(state);

        if (outcome.Kind == AdminPageOutcomeKind.StayOnPage)
        {
            HasError = true;
            ErrorMessage = outcome.ErrorMessage ?? TemplateManagerMessages.GrantFailed;
            return Page();
        }

        TempData["TemplateManagerGrantSummary"] = state.GrantToAllUsersSummary ?? outcome.SuccessMessage;
        return RedirectToPage();
    }

    public IActionResult OnPostCancelAdd() => RedirectToPage();

    public async Task<IActionResult> OnPostClearAllAsync()
    {
        try
        {
            var templateId = HttpContext.Session.GetString("TemplateId");
            HttpContext.Session.Clear();

            if (!string.IsNullOrEmpty(templateId))
            {
                var cacheKey = $"FormTemplate_{CacheKeyHelper.GenerateHashedCacheKey(templateId)}";
                cacheService.Remove(cacheKey);
                logger.LogInformation("Cleared template cache for key: {CacheKey}", cacheKey);
            }

            logger.LogInformation("Successfully cleared all sessions and caches from TemplateManager");
            return RedirectToPage("/Applications/Dashboard");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error clearing sessions and caches from TemplateManager");
            HasError = true;
            ErrorMessage = TemplateManagerMessages.ClearFailed;
            return Page();
        }
    }

    private TemplateManagerWorkState CaptureWorkState() =>
        new()
        {
            SelectedTemplateId = SelectedTemplateId,
            SelectedVersionNumber = SelectedVersionNumber,
            NewVersion = NewVersion,
            NewSchema = NewSchema,
            AcknowledgeReportingImpact = AcknowledgeReportingImpact,
            ShowAddVersionForm = ShowAddVersionForm,
            TenantTemplates = TenantTemplates,
            SessionVersionNumber = HttpContext.Session.GetString(TemplateVersionSessionKey)
        };

    private void ApplyWorkState(TemplateManagerWorkState state)
    {
        SelectedTemplateId = state.SelectedTemplateId;
        SelectedVersionNumber = state.SelectedVersionNumber;
        NewVersion = state.NewVersion ?? NewVersion;
        NewSchema = state.NewSchema ?? NewSchema;
        CurrentTemplate = state.CurrentTemplate;
        CurrentVersionNumber = state.CurrentVersionNumber;
        LatestVersionNumber = state.LatestVersionNumber;
        CurrentTemplateJson = state.CurrentTemplateJson;
        AvailableVersions = state.AvailableVersions;
        SelectedTemplate = state.SelectedTemplate;
        if (state.HasError)
        {
            HasError = true;
            ErrorMessage = state.ErrorMessage;
        }
    }

    private void PersistSessionVersion(TemplateManagerWorkState state)
    {
        if (!string.IsNullOrWhiteSpace(state.SessionVersionNumber))
            HttpContext.Session.SetString(TemplateVersionSessionKey, state.SessionVersionNumber);
    }

    private async Task<IActionResult> RedisplayAddVersionFormAsync(
        TemplateManagerWorkState state,
        Guid templateId,
        AdminPageOutcome outcome)
    {
        ApplyValidationErrors(outcome);
        ShowAddVersionForm = true;
        await templateManagerAdmin.LoadTemplateDataAsync(state, templateId);
        ApplyWorkState(state);
        return Page();
    }

    private void PrefillNewSchema(Guid templateId)
    {
        var schemaWasEmpty = string.IsNullOrWhiteSpace(NewSchema);
        var state = CaptureWorkState();
        state.CurrentTemplateJson = CurrentTemplateJson;
        state.SelectedTemplate = SelectedTemplate;
        templateManagerAdmin.PrefillNewSchemaIfEmpty(state, templateId);
        NewSchema = state.NewSchema;
        NewVersion = state.NewVersion ?? NewVersion;
        // Only clear ModelState when we just prefilled an empty textarea on GET.
        // Removing it after a failed save also wipes the schema validation messages.
        if (ShowAddVersionForm && schemaWasEmpty)
            ModelState.Remove(nameof(NewSchema));
    }

    private void ApplyValidationErrors(AdminPageOutcome validation)
    {
        foreach (var error in validation.Errors)
        {
            if (error.FieldKey == nameof(NewSchema)
                && error.Message == TemplateManagerMessages.SchemaRequired
                && ModelState[nameof(NewSchema)]?.Errors.Count > 0)
            {
                continue;
            }

            ModelState.AddModelError(error.FieldKey, error.Message);
        }
    }

    private async Task LoadTenantTemplatesAsync(CancellationToken cancellationToken = default)
    {
        TenantTemplates = await templateSelectionService.GetSelectableTemplatesAsync(cancellationToken);
    }

    private async Task<Guid?> ResolveSelectedTemplateIdAsync(CancellationToken cancellationToken = default)
    {
        var sessionTemplateId = templateSelectionService.GetSelectedTemplateId(HttpContext);
        if (Guid.TryParse(sessionTemplateId, out var selectedId) &&
            TenantTemplates.Any(template => template.TemplateId == selectedId))
        {
            SelectedTemplateId = selectedId;
            SelectedTemplate = TenantTemplates.First(template => template.TemplateId == selectedId);
            return selectedId;
        }

        var firstTemplate = TenantTemplates.FirstOrDefault();
        if (firstTemplate is null)
            return null;

        await templateSelectionService.SelectTemplateAsync(HttpContext, firstTemplate, cancellationToken);
        SelectedTemplateId = firstTemplate.TemplateId;
        SelectedTemplate = firstTemplate;
        return firstTemplate.TemplateId;
    }

    private async Task InvalidateTemplateCacheAsync(string templateId)
    {
        try
        {
            var cacheKey = $"FormTemplate_{CacheKeyHelper.GenerateHashedCacheKey(templateId)}";
            logger.LogInformation("Attempting to invalidate cache for template {TemplateId} with key {CacheKey}",
                templateId, cacheKey);

            cacheService.Remove(cacheKey);
            logger.LogInformation("Successfully invalidated cache for template {TemplateId} with key {CacheKey}",
                templateId, cacheKey);

            try
            {
                await formTemplateProvider.GetTemplateAsync(templateId);
                logger.LogDebug("Successfully verified new template version is available for {TemplateId}", templateId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to verify new template version for {TemplateId}", templateId);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to invalidate cache for template {TemplateId}", templateId);
        }
    }
}
