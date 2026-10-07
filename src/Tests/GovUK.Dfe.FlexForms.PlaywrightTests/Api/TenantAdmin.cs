using System.Text.Json;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Api;

public static class TenantAdmin
{
    private const string WebTarget = "Web";
    private const string ApplicationTerminologyCategory = "ApplicationTerminology";
    private const string NotificationBannerCategory = "NotificationBanner";
    private const string DashboardCategory = "Dashboard";
    private const string ApplicationPreviewCategory = "ApplicationPreview";

    private static Task UpsertSafeSettingAsync(
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
                SettingsJson = ApiBase.EncodeJson(settingsJson),
                IsSecret = false,
            });

    public static Task RefreshTenantSettingsAsync(IAPIRequestContext request) =>
        ApiBase.ApiRequestAsync<object>(
            request,
            $"/v1/admin/tenants/refresh",
            method: "POST");

    public static Task RestoreApplicationTerminologyAsync(
        IAPIRequestContext request,
        string tenantId,
        string singular,
        string plural) =>
        UpsertSafeSettingAsync(
            request,
            tenantId,
            ApplicationTerminologyCategory,
            JsonSerializer.Serialize(new { Singular = singular, Plural = plural }));

    public static Task ClearNotificationBannerAsync(IAPIRequestContext request, string tenantId) =>
        UpsertSafeSettingAsync(
            request,
            tenantId,
            NotificationBannerCategory,
            JsonSerializer.Serialize(new { Enabled = false, Heading = "Important", Message = "" }));

    public static Task RestoreDashboardAsync(IAPIRequestContext request, string tenantId) =>
        UpsertSafeSettingAsync(
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

    public static Task RestoreApplicationPreviewAsync(IAPIRequestContext request, string tenantId) =>
        UpsertSafeSettingAsync(
            request,
            tenantId,
            ApplicationPreviewCategory,
            JsonSerializer.Serialize(new
            {
                PageHeading = "",
                SubmitHeading = "",
                SubmitHint = "",
                SubmitButtonText = "",
                HideSubmitSection = false
            }));

    public static Task RestoreApplicationSubmittedPageAsync(IAPIRequestContext request, string tenantId) =>
        UpsertSafeSettingAsync(
            request,
            tenantId,
            "ApplicationSubmittedPage",
            JsonSerializer.Serialize(new
            {
                _default = new
                {
                    PanelTitle = "",
                    BodyMarkdown = ""
                }
            }));
}