using System.Text.Json;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Loads and saves non-secret organisation settings (terminology, banner, dashboard, application preview).
/// </summary>
public interface IOrganisationSettingsAdmin
{
    Task LoadAsync(OrganisationSettingsWorkState state, CancellationToken cancellationToken = default);

    Task<AdminPageOutcome> SaveAsync(OrganisationSettingsWorkState state, CancellationToken cancellationToken = default);
}

public sealed class OrganisationSettingsAdminService(
    ITenantAdminClient tenantAdminClient,
    ILogger<OrganisationSettingsAdminService> logger) : IOrganisationSettingsAdmin
{
    private const string TargetWeb = "Web";
    private const string CategoryTerminology = "ApplicationTerminology";
    private const string CategoryBanner = "NotificationBanner";
    private const string CategoryDashboard = "Dashboard";
    private const string CategoryApplicationPreview = "ApplicationPreview";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public async Task LoadAsync(OrganisationSettingsWorkState state, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await tenantAdminClient.GetSafeTenantSettingsAsync(state.TenantId, cancellationToken);
            state.TenantName = response.TenantName;

            foreach (var setting in response.Settings ?? [])
            {
                ApplySettingJson(state, setting.Category, setting.SettingsJson);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load organisation settings for {TenantId}", state.TenantId);
            state.HasError = true;
            state.ErrorMessage = AdminApiErrorMapper.Format(
                ex,
                OrganisationSettingsMessages.LoadFailed,
                includeGatewayHint: false);
        }
    }

    public async Task<AdminPageOutcome> SaveAsync(
        OrganisationSettingsWorkState state,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await UpsertCategoryAsync(
                state.TenantId,
                CategoryTerminology,
                new { Singular = state.TerminologySingular, Plural = state.TerminologyPlural },
                cancellationToken);

            await UpsertCategoryAsync(
                state.TenantId,
                CategoryBanner,
                new { Enabled = state.BannerEnabled, Heading = state.BannerHeading, Message = state.BannerMessage },
                cancellationToken);

            await UpsertCategoryAsync(
                state.TenantId,
                CategoryDashboard,
                new
                {
                    PageSize = state.DashboardPageSize,
                    EnableApplicationFilters = state.DashboardEnableFilters,
                    MainHeading = state.DashboardMainHeading,
                    InProgressHeading = state.DashboardInProgressHeading,
                    StartNewHeading = state.DashboardStartNewHeading,
                    StartNewHint = state.DashboardStartNewHint,
                    StartNewButtonText = state.DashboardStartNewButtonText
                },
                cancellationToken);

            await UpsertCategoryAsync(
                state.TenantId,
                CategoryApplicationPreview,
                new
                {
                    PageHeading = state.PreviewPageHeading,
                    SubmitHeading = state.PreviewSubmitHeading,
                    SubmitHint = state.PreviewSubmitHint,
                    SubmitButtonText = state.PreviewSubmitButtonText,
                    HideSubmitSection = state.PreviewHideSubmitSection
                },
                cancellationToken);

            await tenantAdminClient.RefreshTenantConfigurationAsync(cancellationToken);

            return AdminPageOutcome.Redirect(
                successMessage: OrganisationSettingsMessages.Saved,
                refreshLocalCaches: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save organisation settings for {TenantId}", state.TenantId);
            var message = AdminApiErrorMapper.Format(
                ex,
                OrganisationSettingsMessages.SaveFailed,
                includeGatewayHint: false);
            state.HasError = true;
            state.ErrorMessage = message;
            return AdminPageOutcome.Stay(errorMessage: message);
        }
    }

    private async Task UpsertCategoryAsync(
        Guid tenantId,
        string category,
        object payload,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await tenantAdminClient.UpsertSafeTenantSettingAsync(
            tenantId,
            new UpsertTenantSettingRequest(category, TargetWeb, AdminSettingsEncoding.ToBase64(json), IsSecret: false),
            cancellationToken);
    }

    private void ApplySettingJson(OrganisationSettingsWorkState state, string category, string? settingsJson)
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
                    state.TerminologySingular = singular;
                if (TryGetString(root, "Plural", out var plural))
                    state.TerminologyPlural = plural;
            }
            else if (string.Equals(category, CategoryBanner, StringComparison.OrdinalIgnoreCase))
            {
                if (TryGetBool(root, "Enabled", out var enabled))
                    state.BannerEnabled = enabled;
                if (TryGetString(root, "Heading", out var heading))
                    state.BannerHeading = heading;
                if (TryGetString(root, "Message", out var message))
                    state.BannerMessage = message;
            }
            else if (string.Equals(category, CategoryDashboard, StringComparison.OrdinalIgnoreCase))
            {
                if (TryGetInt(root, "PageSize", out var pageSize))
                    state.DashboardPageSize = pageSize;
                if (TryGetBool(root, "EnableApplicationFilters", out var filters))
                    state.DashboardEnableFilters = filters;
                if (TryGetString(root, "MainHeading", out var mainHeading))
                    state.DashboardMainHeading = mainHeading;
                if (TryGetString(root, "InProgressHeading", out var inProgressHeading))
                    state.DashboardInProgressHeading = inProgressHeading;
                if (TryGetString(root, "StartNewHeading", out var startNewHeading))
                    state.DashboardStartNewHeading = startNewHeading;
                if (TryGetString(root, "StartNewHint", out var startNewHint))
                    state.DashboardStartNewHint = startNewHint;
                if (TryGetString(root, "StartNewButtonText", out var startNewButtonText))
                    state.DashboardStartNewButtonText = startNewButtonText;
            }
            else if (string.Equals(category, CategoryApplicationPreview, StringComparison.OrdinalIgnoreCase))
            {
                if (TryGetString(root, "PageHeading", out var pageHeading))
                    state.PreviewPageHeading = pageHeading;
                if (TryGetString(root, "SubmitHeading", out var submitHeading))
                    state.PreviewSubmitHeading = submitHeading;
                if (TryGetString(root, "SubmitHint", out var submitHint))
                    state.PreviewSubmitHint = submitHint;
                if (TryGetString(root, "SubmitButtonText", out var submitButtonText))
                    state.PreviewSubmitButtonText = submitButtonText;
                if (TryGetBool(root, "HideSubmitSection", out var hideSubmit))
                    state.PreviewHideSubmitSection = hideSubmit;
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
}
