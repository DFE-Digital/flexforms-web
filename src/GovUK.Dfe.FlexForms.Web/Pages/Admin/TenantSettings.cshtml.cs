using System.Text;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.CoreLibs.Http.Models;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Web.Services.Tenant;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Tenant Admin / SuperAdmin editor for TenantConfig app settings (current tenant).
/// </summary>
[Authorize(Policy = AdminAccessHelper.CanManageTenantSettingsPolicy)]
public sealed class TenantSettingsModel(
    ITenantAdminClient tenantAdminClient,
    ITenantRequestContext tenantRequestContext,
    ITenantConfigurationCache tenantConfigurationCache,
    ITenantIdResolver tenantIdResolver,
    ILogger<TenantSettingsModel> logger) : PageModel
{
    public static readonly string[] ValidTargets = ["Shared", "Api", "Web"];

    public Guid TenantId { get; private set; }

    public string TenantName { get; private set; } = string.Empty;

    public IReadOnlyList<TenantSettingDto> Settings { get; private set; } = [];

    public TenantEffectiveConfigurationDto? EffectiveConfig { get; private set; }

    public TenantHealthDto? TenantHealth { get; private set; }

    public IReadOnlyList<TenantSettingCategoryCookbookEntryDto> Cookbook { get; private set; } = [];

    public IReadOnlyList<TenantSettingAuditEntryDto> AuditEntries { get; private set; } = [];

    public ValidateTenantSettingResponse? ValidationPreview { get; private set; }

    public string? ValidationCategory { get; private set; }

    public string? ValidationTarget { get; private set; }

    public bool ValidationIsSecret { get; private set; }

    public bool HasError { get; private set; }

    public string? ErrorMessage { get; private set; }

    public bool ShowSuccess { get; private set; }

    public string? SuccessMessage { get; private set; }

    [BindProperty]
    public string NewCategory { get; set; } = string.Empty;

    [BindProperty]
    public string NewTarget { get; set; } = "Shared";

    [BindProperty]
    public string NewSettingsJson { get; set; } = "{}";

    [BindProperty]
    public bool NewIsSecret { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        ApplyTempData();
        if (!TryResolveTenant(out var error))
        {
            HasError = true;
            ErrorMessage = error;
            return Page();
        }

        await LoadSettingsAsync(cancellationToken);
        await LoadHealthAsync(cancellationToken);
        await LoadCookbookAsync(cancellationToken);
        await LoadAuditLogAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostValidateAsync(
        string category,
        string target,
        string settingsJson,
        bool isSecret,
        CancellationToken cancellationToken)
    {
        ApplyTempData();
        if (!TryResolveTenant(out var error))
        {
            HasError = true;
            ErrorMessage = error;
            return Page();
        }

        category = category?.Trim() ?? string.Empty;
        target = target?.Trim() ?? string.Empty;
        settingsJson = settingsJson?.Trim() ?? string.Empty;
        ValidationCategory = category;
        ValidationTarget = target;
        ValidationIsSecret = isSecret;

        await LoadSettingsAsync(cancellationToken);
        await LoadHealthAsync(cancellationToken);
        await LoadCookbookAsync(cancellationToken);
        await LoadAuditLogAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(settingsJson))
        {
            HasError = true;
            ErrorMessage = "Category and settings JSON are required to validate.";
            return Page();
        }

        try
        {
            ValidationPreview = await tenantAdminClient.ValidateTenantSettingAsync(
                TenantId,
                new ValidateTenantSettingRequest(category, target, ToBase64SettingsJson(settingsJson), isSecret),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to validate tenant setting {Category}/{Target}", category, target);
            HasError = true;
            ErrorMessage = GetErrorMessage(ex, "Could not validate setting.");
        }

        return Page();
    }

    public Task<IActionResult> OnPostValidateNewAsync(CancellationToken cancellationToken)
        => OnPostValidateAsync(NewCategory, NewTarget, NewSettingsJson, NewIsSecret, cancellationToken);

    public async Task<IActionResult> OnPostDeleteAsync(
        string category,
        string target,
        CancellationToken cancellationToken)
    {
        if (!TryResolveTenant(out var error))
        {
            TempData["TenantSettingsError"] = error;
            return RedirectToPage();
        }

        category = category?.Trim() ?? string.Empty;
        target = target?.Trim() ?? string.Empty;

        try
        {
            await tenantAdminClient.DeleteTenantSettingAsync(TenantId, category, target, cancellationToken);
            await RefreshCachesAsync(cancellationToken);
            TempData["TenantSettingsSuccess"] = $"Deleted '{category}' ({target}).";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete tenant setting {Category}/{Target}", category, target);
            TempData["TenantSettingsError"] = GetErrorMessage(ex, "Could not delete setting.");
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateAsync(
        string category,
        string target,
        string settingsJson,
        bool isSecret,
        CancellationToken cancellationToken)
    {
        if (!TryResolveTenant(out var error))
        {
            TempData["TenantSettingsError"] = error;
            return RedirectToPage();
        }

        category = category?.Trim() ?? string.Empty;
        target = target?.Trim() ?? string.Empty;
        settingsJson = settingsJson?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(settingsJson))
        {
            TempData["TenantSettingsError"] = "Category and settings JSON are required.";
            return RedirectToPage();
        }

        if (!ValidTargets.Contains(target, StringComparer.OrdinalIgnoreCase))
        {
            TempData["TenantSettingsError"] = "Target must be Shared, Api, or Web.";
            return RedirectToPage();
        }

        try
        {
            await tenantAdminClient.UpsertTenantSettingAsync(
                TenantId,
                new UpsertTenantSettingRequest(category, target, ToBase64SettingsJson(settingsJson), isSecret),
                cancellationToken);

            await RefreshCachesAsync(cancellationToken);

            TempData["TenantSettingsSuccess"] = $"Updated '{category}' ({target}).";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update tenant setting {Category}/{Target}", category, target);
            TempData["TenantSettingsError"] = GetErrorMessage(ex, "Could not update setting.");
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddAsync(CancellationToken cancellationToken)
    {
        if (!TryResolveTenant(out var error))
        {
            TempData["TenantSettingsError"] = error;
            return RedirectToPage();
        }

        NewCategory = NewCategory?.Trim() ?? string.Empty;
        NewTarget = NewTarget?.Trim() ?? "Shared";
        NewSettingsJson = NewSettingsJson?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(NewCategory))
        {
            TempData["TenantSettingsError"] = "Enter a category name.";
            return RedirectToPage();
        }

        if (NewCategory.Length > 50)
        {
            TempData["TenantSettingsError"] = "Category must not exceed 50 characters.";
            return RedirectToPage();
        }

        if (!ValidTargets.Contains(NewTarget, StringComparer.OrdinalIgnoreCase))
        {
            TempData["TenantSettingsError"] = "Target must be Shared, Api, or Web.";
            return RedirectToPage();
        }

        if (string.IsNullOrWhiteSpace(NewSettingsJson))
        {
            TempData["TenantSettingsError"] = "Enter settings JSON.";
            return RedirectToPage();
        }

        try
        {
            await tenantAdminClient.UpsertTenantSettingAsync(
                TenantId,
                new UpsertTenantSettingRequest(NewCategory, NewTarget, ToBase64SettingsJson(NewSettingsJson), NewIsSecret),
                cancellationToken);

            await RefreshCachesAsync(cancellationToken);

            TempData["TenantSettingsSuccess"] = $"Added '{NewCategory}' ({NewTarget}).";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add tenant setting {Category}/{Target}", NewCategory, NewTarget);
            TempData["TenantSettingsError"] = GetErrorMessage(ex, "Could not add setting.");
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostExportAsync(CancellationToken cancellationToken)
    {
        if (!TryResolveTenant(out var error))
        {
            TempData["TenantSettingsError"] = error;
            return RedirectToPage();
        }

        try
        {
            var export = await tenantAdminClient.ExportConfigurationAsync(TenantId, cancellationToken);
            var json = System.Text.Json.JsonSerializer.Serialize(export,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            var bytes = Encoding.UTF8.GetBytes(json);
            return File(bytes, "application/json", $"tenant-config-{TenantId:N}-{DateTime.UtcNow:yyyyMMddHHmmss}.json");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to export tenant configuration for {TenantId}", TenantId);
            TempData["TenantSettingsError"] = GetErrorMessage(ex, "Could not export configuration.");
            return RedirectToPage();
        }
    }

    public async Task<IActionResult> OnPostImportAsync(IFormFile? importFile, CancellationToken cancellationToken)
    {
        if (!TryResolveTenant(out var error))
        {
            TempData["TenantSettingsError"] = error;
            return RedirectToPage();
        }

        if (importFile is null || importFile.Length == 0)
        {
            TempData["TenantSettingsError"] = "Select a JSON file to import.";
            return RedirectToPage();
        }

        try
        {
            using var reader = new StreamReader(importFile.OpenReadStream());
            var json = await reader.ReadToEndAsync(cancellationToken);
            var exportBundle = System.Text.Json.JsonSerializer.Deserialize<ExportTenantConfigurationDto>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (exportBundle?.Settings is null || exportBundle.Settings.Count == 0)
            {
                TempData["TenantSettingsError"] = "The import file contains no settings.";
                return RedirectToPage();
            }

            var importItems = exportBundle.Settings
                .Select(s => new TenantSettingImportItemDto(s.Category, s.Target, s.SettingsJson, s.IsSecret))
                .ToList();

            var bundle = new ImportTenantConfigurationDto(importItems, SkipSecretPlaceholders: true);

            var result = await tenantAdminClient.ImportConfigurationAsync(TenantId, bundle, cancellationToken);
            await RefreshCachesAsync(cancellationToken);

            TempData["TenantSettingsSuccess"] =
                $"Imported {result.AppliedCount} settings ({result.SkippedCount} secret placeholders skipped).";
        }
        catch (System.Text.Json.JsonException)
        {
            TempData["TenantSettingsError"] = "The file is not valid JSON.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to import tenant configuration for {TenantId}", TenantId);
            TempData["TenantSettingsError"] = GetErrorMessage(ex, "Could not import configuration.");
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRefreshAsync(CancellationToken cancellationToken)
    {
        if (!TryResolveTenant(out var error))
        {
            TempData["TenantSettingsError"] = error;
            return RedirectToPage();
        }

        try
        {
            await RefreshCachesAsync(cancellationToken);
            TempData["TenantSettingsSuccess"] = "Tenant configuration cache refreshed.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh tenant configuration for {TenantId}", TenantId);
            TempData["TenantSettingsError"] = GetErrorMessage(ex, "Could not refresh settings.");
        }

        return RedirectToPage();
    }

    private async Task RefreshCachesAsync(CancellationToken cancellationToken)
    {
        await tenantAdminClient.RefreshTenantConfigurationAsync(cancellationToken);
        tenantConfigurationCache.Invalidate(TenantId);
        tenantIdResolver.InvalidateHostnameCache();
    }

    private async Task LoadSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await tenantAdminClient.GetTenantSettingsAsync(TenantId, cancellationToken);
            TenantName = response.TenantName;
            Settings = response.Settings?.ToList() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load tenant settings for {TenantId}", TenantId);
            HasError = true;
            ErrorMessage = GetErrorMessage(ex, "Could not load tenant settings.");
            Settings = [];
        }
    }

    private bool TryResolveTenant(out string? error)
    {
        if (tenantRequestContext.TenantId is not { } tenantId || tenantId == Guid.Empty)
        {
            error = "Tenant context is not available for this request.";
            return false;
        }

        TenantId = tenantId;
        TenantName = tenantRequestContext.TenantName ?? string.Empty;
        error = null;
        return true;
    }

    private void ApplyTempData()
    {
        if (TempData["TenantSettingsSuccess"] is string success)
        {
            ShowSuccess = true;
            SuccessMessage = success;
        }

        if (TempData["TenantSettingsError"] is string error)
        {
            HasError = true;
            ErrorMessage = error;
        }
    }

    /// <summary>
    /// Encodes settings JSON as Base64 for the API (WAF-safe; mirrors template schema transport).
    /// </summary>
    internal static string ToBase64SettingsJson(string settingsJson) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(settingsJson));

    private async Task LoadHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            TenantHealth = await tenantAdminClient.GetTenantHealthAsync(TenantId, cancellationToken);
            EffectiveConfig = TenantHealth.EffectiveConfiguration;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load tenant health for {TenantId}", TenantId);
            try
            {
                EffectiveConfig = await tenantAdminClient.GetEffectiveConfigurationAsync(TenantId, cancellationToken);
            }
            catch (Exception inner)
            {
                logger.LogWarning(inner, "Failed to load effective configuration for {TenantId}", TenantId);
            }
        }
    }

    private async Task LoadCookbookAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await tenantAdminClient.GetCategoryCookbookAsync(cancellationToken);
            Cookbook = response.Categories?.ToList() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load category cookbook");
        }
    }

    private async Task LoadAuditLogAsync(CancellationToken cancellationToken)
    {
        try
        {
            var log = await tenantAdminClient.GetSettingAuditLogAsync(TenantId, 20, cancellationToken);
            AuditEntries = log?.Entries?.ToList() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load audit log for {TenantId}", TenantId);
        }
    }

    internal static string GetErrorMessage(Exception ex, string fallback)
    {
        if (ex is ExternalApplicationsException<ExceptionResponse> apiEx
            && !string.IsNullOrWhiteSpace(apiEx.Result?.Message))
        {
            return apiEx.Result.Message;
        }

        if (ex is ExternalApplicationsException clientEx)
        {
            var body = clientEx.Response?.TrimStart() ?? string.Empty;
            if (clientEx.StatusCode == 403 && body.StartsWith('<'))
            {
                return "Save was blocked with HTTP 403 (HTML response). "
                    + "This usually means an Azure gateway/WAF rejected the request before the API. "
                    + "Check Front Door / App Gateway logs for /v1/admin/tenants/.../settings.";
            }

            if (clientEx.StatusCode > 0)
                return $"{fallback} (HTTP {clientEx.StatusCode})";
        }

        return fallback;
    }
}
