using System.Text;
using System.Text.Json;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Api;

public static class TenantAdmin
{
    private const string WebTarget = "Web";
    private const string ApplicationTerminologyCategory = "ApplicationTerminology";
    private const string NotificationBannerCategory = "NotificationBanner";
    private const string DashboardCategory = "Dashboard";

    public static Task UpsertSafeSettingAsync(
        IAPIRequestContext request,
        string tenantId,
        string category,
        string settingsJson) =>
        ApiBase.ApiRequestAsync<object>(
            request,
            $"/v1/admin/tenants/{tenantId}/safe-settings",
            method: "PUT",
            data: new
            {
                Category = category,
                Target = WebTarget,
                SettingsJson = Convert.ToBase64String(Encoding.UTF8.GetBytes(settingsJson)),
                IsSecret = false,
            });

    private static Task RefreshTenantSettingsAsync(IAPIRequestContext request) =>
        ApiBase.ApiRequestAsync<object>(
            request,
            $"/v1/admin/tenants/refresh",
            method: "POST");

    public static async Task RestoreApplicationTerminologyAsync(
        IAPIRequestContext request,
        string tenantId,
        string singular,
        string plural)
    {
        await UpsertSafeSettingAsync(
            request,
            tenantId,
            ApplicationTerminologyCategory,
            JsonSerializer.Serialize(new { Singular = singular, Plural = plural }));
        await RefreshTenantSettingsAsync(request);
    }

    public static async Task ClearNotificationBannerAsync(IAPIRequestContext request, string tenantId)
    {
        await UpsertSafeSettingAsync(
            request,
            tenantId,
            NotificationBannerCategory,
            JsonSerializer.Serialize(new { Enabled = false, Heading = "Important", Message = "" }));
        await RefreshTenantSettingsAsync(request);
    }

    public static async Task RestoreDashboardAsync(IAPIRequestContext request, string tenantId)
    {
        await UpsertSafeSettingAsync(
            request,
            tenantId,
            DashboardCategory,
            JsonSerializer.Serialize(new
            {
                PageSize = 50,
                EnableApplicationFilters = true,
                MainHeading = "",
                InProgressHeading = "",
                StartNewHeading = "",
                StartNewHint = "",
                StartNewButtonText = ""
            }));
        await RefreshTenantSettingsAsync(request);
    }
}