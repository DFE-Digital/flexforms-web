using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
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
/// Tenant Admin editor for non-secret organisation settings (terminology, banner, dashboard).
/// </summary>
[Authorize(Roles = AdminAccessHelper.AuthorizeRoles)]
public sealed class OrganisationSettingsModel(
    ITenantAdminClient tenantAdminClient,
    ITenantRequestContext tenantRequestContext,
    ITenantConfigurationCache tenantConfigurationCache,
    ITenantIdResolver tenantIdResolver,
    ILogger<OrganisationSettingsModel> logger) : PageModel
{
    private const string TargetWeb = "Web";
    private const string CategoryTerminology = "ApplicationTerminology";
    private const string CategoryBanner = "NotificationBanner";
    private const string CategoryDashboard = "Dashboard";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public Guid TenantId { get; private set; }

    public string TenantName { get; private set; } = string.Empty;

    public bool HasError { get; private set; }

    public string? ErrorMessage { get; private set; }

    public bool ShowSuccess { get; private set; }

    public string? SuccessMessage { get; private set; }

    [BindProperty]
    [Required(ErrorMessage = "Enter the singular term")]
    [StringLength(100)]
    public string TerminologySingular { get; set; } = "application";

    [BindProperty]
    [Required(ErrorMessage = "Enter the plural term")]
    [StringLength(100)]
    public string TerminologyPlural { get; set; } = "applications";

    [BindProperty]
    public bool BannerEnabled { get; set; }

    [BindProperty]
    [StringLength(200)]
    public string? BannerHeading { get; set; } = "Important";

    [BindProperty]
    [StringLength(2000)]
    public string? BannerMessage { get; set; } = string.Empty;

    [BindProperty]
    [Range(1, 500, ErrorMessage = "Page size must be between 1 and 500")]
    public int DashboardPageSize { get; set; } = 50;

    [BindProperty]
    public bool DashboardEnableFilters { get; set; }

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

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!TryResolveTenant(out var error))
        {
            HasError = true;
            ErrorMessage = error;
            return Page();
        }

        if (!ModelState.IsValid)
            return Page();

        TerminologySingular = TerminologySingular?.Trim() ?? string.Empty;
        TerminologyPlural = TerminologyPlural?.Trim() ?? string.Empty;
        BannerHeading = BannerHeading?.Trim() ?? "Important";
        BannerMessage = BannerMessage?.Trim() ?? string.Empty;

        try
        {
            await UpsertCategoryAsync(
                CategoryTerminology,
                new { Singular = TerminologySingular, Plural = TerminologyPlural },
                cancellationToken);

            await UpsertCategoryAsync(
                CategoryBanner,
                new { Enabled = BannerEnabled, Heading = BannerHeading, Message = BannerMessage },
                cancellationToken);

            await UpsertCategoryAsync(
                CategoryDashboard,
                new { PageSize = DashboardPageSize, EnableApplicationFilters = DashboardEnableFilters },
                cancellationToken);

            await RefreshCachesAsync(cancellationToken);

            TempData["OrganisationSettingsSuccess"] = "Organisation settings saved.";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save organisation settings for {TenantId}", TenantId);
            HasError = true;
            ErrorMessage = GetErrorMessage(ex, "Could not save organisation settings.");
            return Page();
        }
    }

    private async Task UpsertCategoryAsync(string category, object payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await tenantAdminClient.UpsertSafeTenantSettingAsync(
            TenantId,
            new UpsertTenantSettingRequest(category, TargetWeb, ToBase64SettingsJson(json), IsSecret: false),
            cancellationToken);
    }

    private async Task LoadSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await tenantAdminClient.GetSafeTenantSettingsAsync(TenantId, cancellationToken);
            TenantName = response.TenantName;

            foreach (var setting in response.Settings ?? [])
            {
                ApplySettingJson(setting.Category, setting.SettingsJson);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load organisation settings for {TenantId}", TenantId);
            HasError = true;
            ErrorMessage = GetErrorMessage(ex, "Could not load organisation settings.");
        }
    }

    private void ApplySettingJson(string category, string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
            return;

        try
        {
            using var doc = JsonDocument.Parse(settingsJson);
            var root = doc.RootElement;

            if (string.Equals(category, CategoryTerminology, StringComparison.OrdinalIgnoreCase))
            {
                if (TryGetString(root, "Singular", out var singular))
                    TerminologySingular = singular;
                if (TryGetString(root, "Plural", out var plural))
                    TerminologyPlural = plural;
            }
            else if (string.Equals(category, CategoryBanner, StringComparison.OrdinalIgnoreCase))
            {
                if (TryGetBool(root, "Enabled", out var enabled))
                    BannerEnabled = enabled;
                if (TryGetString(root, "Heading", out var heading))
                    BannerHeading = heading;
                if (TryGetString(root, "Message", out var message))
                    BannerMessage = message;
            }
            else if (string.Equals(category, CategoryDashboard, StringComparison.OrdinalIgnoreCase))
            {
                if (TryGetInt(root, "PageSize", out var pageSize))
                    DashboardPageSize = pageSize;
                if (TryGetBool(root, "EnableApplicationFilters", out var filters))
                    DashboardEnableFilters = filters;
            }
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Could not parse settings JSON for category {Category}", category);
        }
    }

    private static bool TryGetString(JsonElement root, string name, out string value)
    {
        value = string.Empty;
        if (!TryGetProperty(root, name, out var prop) || prop.ValueKind != JsonValueKind.String)
            return false;
        value = prop.GetString() ?? string.Empty;
        return true;
    }

    private static bool TryGetBool(JsonElement root, string name, out bool value)
    {
        value = false;
        if (!TryGetProperty(root, name, out var prop))
            return false;

        if (prop.ValueKind == JsonValueKind.True || prop.ValueKind == JsonValueKind.False)
        {
            value = prop.GetBoolean();
            return true;
        }

        return false;
    }

    private static bool TryGetInt(JsonElement root, string name, out int value)
    {
        value = 0;
        if (!TryGetProperty(root, name, out var prop) || prop.ValueKind != JsonValueKind.Number)
            return false;
        return prop.TryGetInt32(out value);
    }

    private static bool TryGetProperty(JsonElement root, string name, out JsonElement property)
    {
        if (root.TryGetProperty(name, out property))
            return true;

        foreach (var p in root.EnumerateObject())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                property = p.Value;
                return true;
            }
        }

        property = default;
        return false;
    }

    private async Task RefreshCachesAsync(CancellationToken cancellationToken)
    {
        await tenantAdminClient.RefreshTenantConfigurationAsync(cancellationToken);
        tenantConfigurationCache.Invalidate(TenantId);
        tenantIdResolver.InvalidateHostnameCache();
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
        if (TempData["OrganisationSettingsSuccess"] is string success)
        {
            ShowSuccess = true;
            SuccessMessage = success;
        }

        if (TempData["OrganisationSettingsError"] is string error)
        {
            HasError = true;
            ErrorMessage = error;
        }
    }

    internal static string ToBase64SettingsJson(string settingsJson) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(settingsJson));

    internal static string GetErrorMessage(Exception ex, string fallback)
    {
        if (ex is ExternalApplicationsException<ExceptionResponse> apiEx
            && !string.IsNullOrWhiteSpace(apiEx.Result?.Message))
        {
            return apiEx.Result.Message;
        }

        if (ex is ExternalApplicationsException clientEx && clientEx.StatusCode > 0)
            return $"{fallback} (HTTP {clientEx.StatusCode})";

        return fallback;
    }
}
