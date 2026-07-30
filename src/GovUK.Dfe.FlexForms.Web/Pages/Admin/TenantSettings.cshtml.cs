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
/// SuperAdmin-only editor for TenantConfig app settings (current tenant).
/// </summary>
[Authorize(Policy = AdminAccessHelper.CanManageTenantSettingsPolicy)]
public sealed class TenantSettingsModel(
    ITenantAdminClient tenantAdminClient,
    ITenantRequestContext tenantRequestContext,
    ITenantConfigurationCache tenantConfigurationCache,
    ILogger<TenantSettingsModel> logger) : PageModel
{
    public static readonly string[] ValidTargets = ["Shared", "Api", "Web"];

    public Guid TenantId { get; private set; }

    public string TenantName { get; private set; } = string.Empty;

    public IReadOnlyList<TenantSettingDto> Settings { get; private set; } = [];

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
        return Page();
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
                new UpsertTenantSettingRequest(category, target, settingsJson, isSecret),
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
                new UpsertTenantSettingRequest(NewCategory, NewTarget, NewSettingsJson, NewIsSecret),
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

    internal static string GetErrorMessage(Exception ex, string fallback)
    {
        if (ex is ExternalApplicationsException<ExceptionResponse> apiEx
            && !string.IsNullOrWhiteSpace(apiEx.Result?.Message))
        {
            return apiEx.Result.Message;
        }

        return fallback;
    }
}
