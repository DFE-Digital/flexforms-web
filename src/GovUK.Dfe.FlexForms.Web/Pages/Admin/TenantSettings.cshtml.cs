using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Application.Admin;
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
    ITenantSettingsAdmin tenantSettingsAdmin,
    ITenantRequestContext tenantRequestContext,
    ITenantConfigurationCache tenantConfigurationCache,
    ITenantIdResolver tenantIdResolver) : PageModel
{
    public static readonly string[] ValidTargets = TenantSettingsAdminService.ValidTargets;

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

    public bool IsSuperAdmin => AdminAccessHelper.IsSuperAdmin(User);

    /// <summary>
    /// False for SuperAdmin-only categories (Templates HostMappings, ConnectionStrings, …)
    /// when the current user is not SuperAdmin.
    /// </summary>
    public bool CanEditCategory(string? category) =>
        IsSuperAdmin || !SuperAdminOnlyTenantSettingCategories.IsRestricted(category);

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
            return StayWithTenantError(error);

        var state = CaptureWorkState();
        await tenantSettingsAdmin.LoadAsync(state, cancellationToken);
        ApplyWorkState(state);
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
            return StayWithTenantError(error);

        if (!CanEditCategory(category))
        {
            HasError = true;
            ErrorMessage = $"Only SuperAdmin can change '{category}' settings.";
            var state = CaptureWorkState();
            await tenantSettingsAdmin.LoadAsync(state, cancellationToken);
            ApplyWorkState(state);
            return Page();
        }

        var workState = CaptureWorkState();
        await tenantSettingsAdmin.ValidateAsync(workState, category, target, settingsJson, isSecret, cancellationToken);
        ApplyWorkState(workState);
        return Page();
    }

    public Task<IActionResult> OnPostValidateNewAsync(CancellationToken cancellationToken)
        => OnPostValidateAsync(NewCategory, NewTarget, NewSettingsJson, NewIsSecret, cancellationToken);

    public Task<IActionResult> OnPostDeleteAsync(string category, string target, CancellationToken cancellationToken)
    {
        if (!CanEditCategory(category))
        {
            TempData["TenantSettingsError"] = $"Only SuperAdmin can delete '{category}' settings.";
            return Task.FromResult<IActionResult>(RedirectToPage());
        }

        return DispatchMutationAsync(state => tenantSettingsAdmin.DeleteAsync(state, category, target, cancellationToken));
    }

    public Task<IActionResult> OnPostUpdateAsync(
        string category,
        string target,
        string settingsJson,
        bool isSecret,
        CancellationToken cancellationToken)
    {
        if (!CanEditCategory(category))
        {
            TempData["TenantSettingsError"] = $"Only SuperAdmin can update '{category}' settings.";
            return Task.FromResult<IActionResult>(RedirectToPage());
        }

        return DispatchMutationAsync(state =>
            tenantSettingsAdmin.UpdateAsync(state, category, target, settingsJson, isSecret, cancellationToken));
    }

    public Task<IActionResult> OnPostAddAsync(CancellationToken cancellationToken)
    {
        if (!CanEditCategory(NewCategory))
        {
            TempData["TenantSettingsError"] = $"Only SuperAdmin can add '{NewCategory}' settings.";
            return Task.FromResult<IActionResult>(RedirectToPage());
        }

        return DispatchMutationAsync(state => tenantSettingsAdmin.AddAsync(
            state,
            NewCategory,
            NewTarget,
            NewSettingsJson,
            NewIsSecret,
            cancellationToken));
    }

    public Task<IActionResult> OnPostExportAsync(CancellationToken cancellationToken)
        => DispatchMutationAsync(state => tenantSettingsAdmin.ExportAsync(state, cancellationToken));

    public async Task<IActionResult> OnPostImportAsync(IFormFile? importFile, CancellationToken cancellationToken)
    {
        if (!TryResolveTenant(out var error))
        {
            TempData["TenantSettingsError"] = error;
            return RedirectToPage();
        }

        if (importFile is null || importFile.Length == 0)
        {
            TempData["TenantSettingsError"] = TenantSettingsMessages.ImportFileRequired;
            return RedirectToPage();
        }

        using var reader = new StreamReader(importFile.OpenReadStream());
        var json = await reader.ReadToEndAsync(cancellationToken);
        return await MapOutcomeAsync(await tenantSettingsAdmin.ImportAsync(CaptureWorkState(), json, cancellationToken));
    }

    public Task<IActionResult> OnPostRefreshAsync(CancellationToken cancellationToken)
        => DispatchMutationAsync(state => tenantSettingsAdmin.RefreshAsync(state, cancellationToken));

    internal static string ToBase64SettingsJson(string settingsJson) =>
        AdminSettingsEncoding.ToBase64(settingsJson);

    internal static string GetErrorMessage(Exception ex, string fallback) =>
        AdminApiErrorMapper.Format(ex, fallback, includeGatewayHint: true);

    private async Task<IActionResult> DispatchMutationAsync(Func<TenantSettingsWorkState, Task<AdminPageOutcome>> execute)
    {
        if (!TryResolveTenant(out var error))
        {
            TempData["TenantSettingsError"] = error;
            return RedirectToPage();
        }

        return await MapOutcomeAsync(await execute(CaptureWorkState()));
    }

    private IActionResult StayWithTenantError(string? error)
    {
        HasError = true;
        ErrorMessage = error;
        return Page();
    }

    private TenantSettingsWorkState CaptureWorkState() =>
        new()
        {
            TenantId = TenantId,
            TenantName = TenantName
        };

    private void ApplyWorkState(TenantSettingsWorkState state)
    {
        TenantId = state.TenantId;
        TenantName = state.TenantName;
        Settings = state.Settings;
        EffectiveConfig = state.EffectiveConfig;
        TenantHealth = state.TenantHealth;
        Cookbook = state.Cookbook;
        AuditEntries = state.AuditEntries;
        ValidationPreview = state.ValidationPreview;
        ValidationCategory = state.ValidationCategory;
        ValidationTarget = state.ValidationTarget;
        ValidationIsSecret = state.ValidationIsSecret;
        if (state.HasError)
        {
            HasError = true;
            ErrorMessage = state.ErrorMessage;
        }
    }

    private Task<IActionResult> MapOutcomeAsync(AdminPageOutcome outcome)
    {
        if (outcome.RefreshLocalCaches)
        {
            tenantConfigurationCache.Invalidate(TenantId);
            tenantIdResolver.InvalidateHostnameCache();
        }

        if (outcome.SuccessMessage != null)
            TempData["TenantSettingsSuccess"] = outcome.SuccessMessage;

        if (outcome.ErrorMessage != null)
            TempData["TenantSettingsError"] = outcome.ErrorMessage;

        IActionResult result = outcome.Kind switch
        {
            AdminPageOutcomeKind.FileDownload => File(
                outcome.FileBytes!,
                outcome.FileContentType!,
                outcome.FileDownloadName),
            AdminPageOutcomeKind.StayOnPage => Page(),
            _ => RedirectToPage()
        };

        return Task.FromResult(result);
    }

    private bool TryResolveTenant(out string? error)
    {
        if (tenantRequestContext.TenantId is not { } tenantId || tenantId == Guid.Empty)
        {
            error = TenantSettingsMessages.TenantContextMissing;
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
}
