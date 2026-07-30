using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Domain.Templates;
using GovUK.Dfe.FlexForms.Web.Services;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using Task = System.Threading.Tasks.Task;
using GovUK.Dfe.CoreLibs.Caching.Interfaces;
using GovUK.Dfe.CoreLibs.Caching.Helpers;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

[Authorize(Policy = AdminAccessHelper.CanManageTemplatesPolicy)]
[RequestSizeLimit(52_428_800)]
[RequestFormLimits(ValueLengthLimit = 52_428_800, ValueCountLimit = 1000)]
public class TemplateManagerModel(
    IFormTemplateProvider formTemplateProvider,
    ITemplatesClient templatesClient,
    ITemplateSelectionService templateSelectionService,
    ICacheService<IMemoryCacheType> cacheService,
    ITemplateValidationService templateValidationService,
    ILogger<TemplateManagerModel> logger) : PageModel
{
    private readonly IFormTemplateProvider _formTemplateProvider = formTemplateProvider;
    private readonly ITemplatesClient _templatesClient = templatesClient;
    private readonly ITemplateSelectionService _templateSelectionService = templateSelectionService;
    private readonly ICacheService<IMemoryCacheType> _cacheService = cacheService;
    private readonly ITemplateValidationService _templateValidationService = templateValidationService;
    private readonly ILogger<TemplateManagerModel> _logger = logger;

    public FormTemplate? CurrentTemplate { get; set; }
    public string? CurrentVersionNumber { get; set; }
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
    public TemplateDto? SelectedTemplate { get; private set; }

    [BindProperty]
    public Guid? SelectedTemplateId { get; set; }

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
        bool granted = false,
        string? suggestedVersion = null,
        string? grantSummary = null)
    {
        try
        {
            _logger.LogInformation("TemplateManager GET started. Memory: {MemoryMB} MB", 
                GC.GetTotalMemory(false) / 1024 / 1024);
            
            ShowAddVersionForm = showForm;
            ShowSuccess = success;
            ShowCacheCleared = cleared;
            ShowCreated = created;
            ShowGrantedToAllUsers = granted;
            GrantToAllUsersSummary = grantSummary;

            await LoadTenantTemplatesAsync();
            var templateId = ResolveSelectedTemplateId();
            if (templateId is null)
            {
                return Page();
            }

            await LoadTemplateDataAsync(templateId.Value);
            
            // If a suggested version is provided, use it to pre-populate the NewVersion field
            if (!string.IsNullOrEmpty(suggestedVersion))
            {
                NewVersion = suggestedVersion;
                _logger.LogInformation("Pre-populated NewVersion field with suggested version: {SuggestedVersion}", suggestedVersion);
            }

            PrefillNewSchemaIfEmpty(templateId.Value);
            
            _logger.LogInformation("TemplateManager GET completed successfully. Memory: {MemoryMB} MB", 
                GC.GetTotalMemory(false) / 1024 / 1024);
            
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CRITICAL ERROR in TemplateManager OnGetAsync. Memory: {MemoryMB} MB, Exception Type: {ExceptionType}", 
                GC.GetTotalMemory(false) / 1024 / 1024, ex.GetType().FullName);
            throw;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadTenantTemplatesAsync();
        var templateId = ResolveSelectedTemplateId();
        if (templateId is null)
        {
            ModelState.AddModelError(string.Empty, "Select a template.");
            return Page();
        }

        if (!ValidateInput())
        {
            ShowAddVersionForm = true;
            await LoadTemplateDataAsync(templateId.Value);
            PrefillNewSchemaIfEmpty(templateId.Value);
            return Page();
        }

        await CreateNewTemplateVersionAsync(templateId.Value.ToString());

        await Task.Delay(2000);

        await InvalidateTemplateCacheAsync(templateId.Value.ToString());

        _logger.LogInformation("Successfully created template version {NewVersion} for {TemplateId}",
            NewVersion, templateId);

        return RedirectToPage(new { success = true });

}

    public async Task<IActionResult> OnPostSelectTemplateAsync(CancellationToken cancellationToken)
    {
        await LoadTenantTemplatesAsync(cancellationToken);

        if (SelectedTemplateId is null ||
            TenantTemplates.All(template => template.TemplateId != SelectedTemplateId.Value))
        {
            ModelState.AddModelError(nameof(SelectedTemplateId), "Select a template for this tenant.");
            return Page();
        }

        var template = TenantTemplates.First(item => item.TemplateId == SelectedTemplateId.Value);
        _templateSelectionService.SelectTemplate(HttpContext, template);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostShowAddFormAsync()
    {
        // Pre-populate the NewVersion field with auto-incremented version
        await LoadTenantTemplatesAsync();
        var templateId = ResolveSelectedTemplateId();
        if (templateId is not null)
        {
            await LoadTemplateDataAsync(templateId.Value);
            
            if (!string.IsNullOrEmpty(CurrentVersionNumber))
            {
                var incrementedVersion = IncrementPatchVersion(CurrentVersionNumber);
                _logger.LogInformation("Auto-incremented version from {CurrentVersion} to {NewVersion}", 
                    CurrentVersionNumber, incrementedVersion);
                
                // Pass the auto-incremented version via query parameter
                return RedirectToPage(new { showForm = true, suggestedVersion = incrementedVersion });
            }
        }
        
        return RedirectToPage(new { showForm = true });
    }

    public async Task<IActionResult> OnPostGrantToAllUsersAsync(CancellationToken cancellationToken)
    {
        await LoadTenantTemplatesAsync(cancellationToken);
        var templateId = ResolveSelectedTemplateId();
        if (templateId is null)
        {
            HasError = true;
            ErrorMessage = "Select a template before granting access to all users.";
            return Page();
        }

        try
        {
            var result = await _templatesClient.GrantTemplateAccessToAllUsersAsync(
                templateId.Value,
                cancellationToken);

            var summary =
                $"Granted to {result.UsersGranted} user(s). " +
                $"{result.UsersAlreadyHadAccess} already had access. " +
                $"Total tenant users checked: {result.TotalUsers}.";

            _logger.LogInformation(
                "Granted template {TemplateId} to all tenant users. Granted={Granted}, AlreadyHad={AlreadyHad}, Total={Total}",
                templateId,
                result.UsersGranted,
                result.UsersAlreadyHadAccess,
                result.TotalUsers);

            return RedirectToPage(new { granted = true, grantSummary = summary });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to grant template {TemplateId} to all tenant users", templateId);
            HasError = true;
            ErrorMessage = "Failed to grant this template to all users in the tenant.";
            await LoadTemplateDataAsync(templateId.Value);
            return Page();
        }
    }
    
    /// <summary>
    /// Increments the patch version of a semantic version string (e.g., 1.0.1 -> 1.0.2)
    /// </summary>
    private static string IncrementPatchVersion(string version)
    {
        try
        {
            var parts = version.Split('.');
            
            if (parts.Length == 0)
            {
                return "1.0.1";
            }
            else if (parts.Length == 1)
            {
                // If only major version exists (e.g., "1"), add minor and patch
                return $"{parts[0]}.0.1";
            }
            else if (parts.Length == 2)
            {
                // If major.minor exists (e.g., "1.0"), add patch as 1
                return $"{parts[0]}.{parts[1]}.1";
            }
            else
            {
                // Full semantic version (e.g., "1.0.1")
                // Increment the patch version
                if (int.TryParse(parts[2], out var patchVersion))
                {
                    patchVersion++;
                    return $"{parts[0]}.{parts[1]}.{patchVersion}";
                }
                else
                {
                    // If patch is not a number, default to adding .1
                    return $"{parts[0]}.{parts[1]}.1";
                }
            }
        }
        catch
        {
            // If anything goes wrong, return a sensible default
            return "1.0.1";
        }
    }

    public IActionResult OnPostCancelAdd()
    {
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostClearAllAsync()
    {
        try
        {
            var templateId = HttpContext.Session.GetString("TemplateId");
            
            // Clear all session data
            HttpContext.Session.Clear();
            
            if (!string.IsNullOrEmpty(templateId))
            {
                var cacheKey = $"FormTemplate_{CacheKeyHelper.GenerateHashedCacheKey(templateId)}";
                _cacheService.Remove(cacheKey);
                _logger.LogInformation("Cleared template cache for key: {CacheKey}", cacheKey);
            }

            _logger.LogInformation("Successfully cleared all sessions and caches from TemplateManager");
            
            // Redirect back to Index since session is cleared (TemplateId is gone)
            return RedirectToPage("/Applications/Dashboard");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing sessions and caches from TemplateManager");
            HasError = true;
            ErrorMessage = "Failed to clear sessions and caches.";
            return Page();
        }
    }

    private async Task LoadTemplateDataAsync(Guid templateId)
    {
        try
        {
            _logger.LogDebug("Loading template data for {TemplateId}", templateId);

            SelectedTemplate = TenantTemplates.First(template => template.TemplateId == templateId);
            SelectedTemplateId = templateId;

            if (string.IsNullOrWhiteSpace(SelectedTemplate.LatestVersionNumber))
            {
                CurrentVersionNumber = null;
                CurrentTemplate = null;
                CurrentTemplateJson = null;
                return;
            }

            var apiResponse = await _templatesClient.GetLatestTemplateSchemaAsync(templateId);
            CurrentVersionNumber = apiResponse.VersionNumber;
            
            _logger.LogDebug("API returned template version {VersionNumber} for {TemplateId}", 
                CurrentVersionNumber, templateId);
            
            // Clear cache before loading to ensure we get the latest template
            var templateIdText = templateId.ToString();
            var cacheKey = $"FormTemplate_{CacheKeyHelper.GenerateHashedCacheKey(templateIdText)}";
            _cacheService.Remove(cacheKey);
            _logger.LogDebug("Cleared template cache for {TemplateId} to ensure latest version is loaded", templateId);
            
            CurrentTemplate = await _formTemplateProvider.GetTemplateAsync(templateIdText);
            if (CurrentTemplate != null)
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                CurrentTemplateJson = JsonSerializer.Serialize(CurrentTemplate, options);
                
                _logger.LogDebug("Successfully loaded template {TemplateId} with {TaskGroupCount} task groups", 
                    templateId, CurrentTemplate.TaskGroups?.Count ?? 0);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading template data for {TemplateId}", templateId);
            HasError = true;
            ErrorMessage = "There was an error loading the template data.";
        }
    }

    private async Task LoadTenantTemplatesAsync(CancellationToken cancellationToken = default)
    {
        TenantTemplates = await _templateSelectionService.GetSelectableTemplatesAsync(cancellationToken);
    }

    private Guid? ResolveSelectedTemplateId()
    {
        var sessionTemplateId = _templateSelectionService.GetSelectedTemplateId(HttpContext);
        if (Guid.TryParse(sessionTemplateId, out var selectedId) &&
            TenantTemplates.Any(template => template.TemplateId == selectedId))
        {
            SelectedTemplateId = selectedId;
            SelectedTemplate = TenantTemplates.First(template => template.TemplateId == selectedId);
            return selectedId;
        }

        var firstTemplate = TenantTemplates.FirstOrDefault();
        if (firstTemplate is null)
        {
            return null;
        }

        _templateSelectionService.SelectTemplate(HttpContext, firstTemplate);
        SelectedTemplateId = firstTemplate.TemplateId;
        SelectedTemplate = firstTemplate;
        return firstTemplate.TemplateId;
    }

    private void PrefillNewSchemaIfEmpty(Guid templateId)
    {
        if (!ShowAddVersionForm || !string.IsNullOrWhiteSpace(NewSchema))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(CurrentTemplateJson))
        {
            NewSchema = CurrentTemplateJson;
        }
        else
        {
            NewSchema = StarterFormTemplateSchema.CreateJson(
                templateId.ToString(),
                SelectedTemplate?.Name ?? "New template");
            NewVersion ??= StarterFormTemplateSchema.DefaultVersionNumber;
        }

        // Prefill after an empty submit — drop the now-stale required error.
        ModelState.Remove(nameof(NewSchema));
    }

    private bool ValidateInput()
    {
        var isValid = true;

        if (string.IsNullOrWhiteSpace(NewVersion))
        {
            ModelState.AddModelError(nameof(NewVersion), "Version number is required");
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(NewSchema))
        {
            // [Required] already adds this during model binding — avoid a duplicate summary line.
            if (ModelState[nameof(NewSchema)]?.Errors.Count is null or 0)
            {
                ModelState.AddModelError(nameof(NewSchema), "JSON schema is required");
            }

            isValid = false;
        }
        else
        {
            // Validate JSON against FormTemplate domain model
            var (templateIsValid, validationErrors) = _templateValidationService.ValidateTemplateJson(NewSchema);
            
            if (!templateIsValid)
            {
                _logger.LogWarning("Template validation failed with {ErrorCount} errors", validationErrors.Count);
                
                // Add all validation errors to ModelState
                foreach (var error in validationErrors)
                {
                    ModelState.AddModelError(nameof(NewSchema), error);
                }
                
                isValid = false;
            }
            else
            {
                _logger.LogInformation("Template validation passed successfully");
            }
        }

        if (!AcknowledgeReportingImpact)
        {
            ModelState.AddModelError(nameof(AcknowledgeReportingImpact),
                "You must confirm that you understand the reporting impact before saving.");
            isValid = false;
        }

        return isValid;
    }

    private async Task CreateNewTemplateVersionAsync(string templateId)
    {
        var base64Schema = Convert.ToBase64String(Encoding.UTF8.GetBytes(NewSchema!));
        await _templatesClient.CreateTemplateVersionAsync(new Guid(templateId),
            new CreateTemplateVersionRequest(VersionNumber: NewVersion!, JsonSchema: base64Schema));
    }

    private async Task InvalidateTemplateCacheAsync(string templateId)
    {
        try
        {
            var cacheKey = $"FormTemplate_{CacheKeyHelper.GenerateHashedCacheKey(templateId)}";
            _logger.LogInformation("Attempting to invalidate cache for template {TemplateId} with key {CacheKey}", 
                templateId, cacheKey);
            
            _cacheService.Remove(cacheKey);
            _logger.LogInformation("Successfully invalidated cache for template {TemplateId} with key {CacheKey}", 
                templateId, cacheKey);
            
            // Verify the new template version is available by attempting to load it
            await VerifyNewTemplateVersionAsync(templateId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate cache for template {TemplateId}", templateId);
            // Don't throw - cache invalidation failure shouldn't break the operation
        }
    }
    
    private async Task VerifyNewTemplateVersionAsync(string templateId)
    {
        try
        {
            // Try to load the new template version to ensure it's available
            var newTemplate = await _formTemplateProvider.GetTemplateAsync(templateId);
            _logger.LogDebug("Successfully verified new template version is available for {TemplateId}", templateId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to verify new template version for {TemplateId}", templateId);
        }
    }

} 