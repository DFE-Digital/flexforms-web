using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Loads and mutates TenantConfig app settings for the current tenant.
/// </summary>
public interface ITenantSettingsAdmin
{
    Task LoadAsync(TenantSettingsWorkState state, CancellationToken cancellationToken = default);

    Task<AdminPageOutcome> ValidateAsync(
        TenantSettingsWorkState state,
        string? category,
        string? target,
        string? settingsJson,
        bool isSecret,
        CancellationToken cancellationToken = default);

    Task<AdminPageOutcome> DeleteAsync(
        TenantSettingsWorkState state,
        string? category,
        string? target,
        CancellationToken cancellationToken = default);

    Task<AdminPageOutcome> UpdateAsync(
        TenantSettingsWorkState state,
        string? category,
        string? target,
        string? settingsJson,
        bool isSecret,
        CancellationToken cancellationToken = default);

    Task<AdminPageOutcome> AddAsync(
        TenantSettingsWorkState state,
        string? category,
        string? target,
        string? settingsJson,
        bool isSecret,
        CancellationToken cancellationToken = default);

    Task<AdminPageOutcome> ExportAsync(
        TenantSettingsWorkState state,
        CancellationToken cancellationToken = default);

    Task<AdminPageOutcome> ImportAsync(
        TenantSettingsWorkState state,
        string json,
        CancellationToken cancellationToken = default);

    Task<AdminPageOutcome> RefreshAsync(
        TenantSettingsWorkState state,
        CancellationToken cancellationToken = default);
}

public sealed class TenantSettingsAdminService(
    ITenantAdminClient tenantAdminClient,
    ILogger<TenantSettingsAdminService> logger) : ITenantSettingsAdmin
{
    public static readonly string[] ValidTargets = ["Shared", "Api", "Web"];

    public async Task LoadAsync(TenantSettingsWorkState state, CancellationToken cancellationToken = default)
    {
        await LoadSettingsAsync(state, cancellationToken);
        await LoadHealthAsync(state, cancellationToken);
        await LoadCookbookAsync(state, cancellationToken);
        await LoadAuditLogAsync(state, cancellationToken);
    }

    public async Task<AdminPageOutcome> ValidateAsync(
        TenantSettingsWorkState state,
        string? category,
        string? target,
        string? settingsJson,
        bool isSecret,
        CancellationToken cancellationToken = default)
    {
        category = category?.Trim() ?? string.Empty;
        target = target?.Trim() ?? string.Empty;
        settingsJson = settingsJson?.Trim() ?? string.Empty;
        state.ValidationCategory = category;
        state.ValidationTarget = target;
        state.ValidationIsSecret = isSecret;

        await LoadAsync(state, cancellationToken);

        if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(settingsJson))
        {
            state.HasError = true;
            state.ErrorMessage = TenantSettingsMessages.ValidateRequired;
            return AdminPageOutcome.Stay(errorMessage: TenantSettingsMessages.ValidateRequired);
        }

        try
        {
            state.ValidationPreview = await tenantAdminClient.ValidateTenantSettingAsync(
                state.TenantId,
                new ValidateTenantSettingRequest(
                    category,
                    target,
                    AdminSettingsEncoding.ToBase64(settingsJson),
                    isSecret),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to validate tenant setting {Category}/{Target}", category, target);
            var message = AdminApiErrorMapper.Format(ex, TenantSettingsMessages.ValidateFailed, includeGatewayHint: true);
            state.HasError = true;
            state.ErrorMessage = message;
            return AdminPageOutcome.Stay(errorMessage: message);
        }

        return AdminPageOutcome.Stay();
    }

    public async Task<AdminPageOutcome> DeleteAsync(
        TenantSettingsWorkState state,
        string? category,
        string? target,
        CancellationToken cancellationToken = default)
    {
        category = category?.Trim() ?? string.Empty;
        target = target?.Trim() ?? string.Empty;

        try
        {
            await tenantAdminClient.DeleteTenantSettingAsync(state.TenantId, category, target, cancellationToken);
            await tenantAdminClient.RefreshTenantConfigurationAsync(cancellationToken);
            return AdminPageOutcome.Redirect(
                successMessage: TenantSettingsMessages.Deleted(category, target),
                refreshLocalCaches: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete tenant setting {Category}/{Target}", category, target);
            return AdminPageOutcome.Redirect(
                errorMessage: AdminApiErrorMapper.Format(ex, TenantSettingsMessages.DeleteFailed, includeGatewayHint: true));
        }
    }

    public async Task<AdminPageOutcome> UpdateAsync(
        TenantSettingsWorkState state,
        string? category,
        string? target,
        string? settingsJson,
        bool isSecret,
        CancellationToken cancellationToken = default)
    {
        category = category?.Trim() ?? string.Empty;
        target = target?.Trim() ?? string.Empty;
        settingsJson = settingsJson?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(settingsJson))
            return AdminPageOutcome.Redirect(errorMessage: TenantSettingsMessages.CategoryAndJsonRequired);

        if (!ValidTargets.Contains(target, StringComparer.OrdinalIgnoreCase))
            return AdminPageOutcome.Redirect(errorMessage: TenantSettingsMessages.InvalidTarget);

        try
        {
            await tenantAdminClient.UpsertTenantSettingAsync(
                state.TenantId,
                new UpsertTenantSettingRequest(category, target, AdminSettingsEncoding.ToBase64(settingsJson), isSecret),
                cancellationToken);
            await tenantAdminClient.RefreshTenantConfigurationAsync(cancellationToken);
            return AdminPageOutcome.Redirect(
                successMessage: TenantSettingsMessages.Updated(category, target),
                refreshLocalCaches: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update tenant setting {Category}/{Target}", category, target);
            return AdminPageOutcome.Redirect(
                errorMessage: AdminApiErrorMapper.Format(ex, TenantSettingsMessages.UpdateFailed, includeGatewayHint: true));
        }
    }

    public async Task<AdminPageOutcome> AddAsync(
        TenantSettingsWorkState state,
        string? category,
        string? target,
        string? settingsJson,
        bool isSecret,
        CancellationToken cancellationToken = default)
    {
        category = category?.Trim() ?? string.Empty;
        target = string.IsNullOrWhiteSpace(target) ? "Shared" : target.Trim();
        settingsJson = settingsJson?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(category))
            return AdminPageOutcome.Redirect(errorMessage: TenantSettingsMessages.CategoryRequired);

        if (category.Length > 50)
            return AdminPageOutcome.Redirect(errorMessage: TenantSettingsMessages.CategoryTooLong);

        if (!ValidTargets.Contains(target, StringComparer.OrdinalIgnoreCase))
            return AdminPageOutcome.Redirect(errorMessage: TenantSettingsMessages.InvalidTarget);

        if (string.IsNullOrWhiteSpace(settingsJson))
            return AdminPageOutcome.Redirect(errorMessage: TenantSettingsMessages.SettingsJsonRequired);

        try
        {
            await tenantAdminClient.UpsertTenantSettingAsync(
                state.TenantId,
                new UpsertTenantSettingRequest(category, target, AdminSettingsEncoding.ToBase64(settingsJson), isSecret),
                cancellationToken);
            await tenantAdminClient.RefreshTenantConfigurationAsync(cancellationToken);
            return AdminPageOutcome.Redirect(
                successMessage: TenantSettingsMessages.Added(category, target),
                refreshLocalCaches: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add tenant setting {Category}/{Target}", category, target);
            return AdminPageOutcome.Redirect(
                errorMessage: AdminApiErrorMapper.Format(ex, TenantSettingsMessages.AddFailed, includeGatewayHint: true));
        }
    }

    public async Task<AdminPageOutcome> ExportAsync(
        TenantSettingsWorkState state,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var export = await tenantAdminClient.ExportConfigurationAsync(state.TenantId, cancellationToken);
            var json = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
            var bytes = Encoding.UTF8.GetBytes(json);
            return AdminPageOutcome.File(
                bytes,
                "application/json",
                $"tenant-config-{state.TenantId:N}-{DateTime.UtcNow:yyyyMMddHHmmss}.json");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to export tenant configuration for {TenantId}", state.TenantId);
            return AdminPageOutcome.Redirect(
                errorMessage: AdminApiErrorMapper.Format(ex, TenantSettingsMessages.ExportFailed, includeGatewayHint: true));
        }
    }

    public async Task<AdminPageOutcome> ImportAsync(
        TenantSettingsWorkState state,
        string json,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var exportBundle = JsonSerializer.Deserialize<ExportTenantConfigurationDto>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (exportBundle?.Settings is null || exportBundle.Settings.Count == 0)
                return AdminPageOutcome.Redirect(errorMessage: TenantSettingsMessages.ImportEmpty);

            var importItems = exportBundle.Settings
                .Select(s => new TenantSettingImportItemDto(s.Category, s.Target, s.SettingsJson, s.IsSecret))
                .ToList();

            var bundle = new ImportTenantConfigurationDto(importItems, SkipSecretPlaceholders: true);
            var result = await tenantAdminClient.ImportConfigurationAsync(state.TenantId, bundle, cancellationToken);
            await tenantAdminClient.RefreshTenantConfigurationAsync(cancellationToken);

            return AdminPageOutcome.Redirect(
                successMessage: TenantSettingsMessages.Imported(result.AppliedCount, result.SkippedCount),
                refreshLocalCaches: true);
        }
        catch (JsonException)
        {
            return AdminPageOutcome.Redirect(errorMessage: TenantSettingsMessages.ImportInvalidJson);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to import tenant configuration for {TenantId}", state.TenantId);
            return AdminPageOutcome.Redirect(
                errorMessage: AdminApiErrorMapper.Format(ex, TenantSettingsMessages.ImportFailed, includeGatewayHint: true));
        }
    }

    public async Task<AdminPageOutcome> RefreshAsync(
        TenantSettingsWorkState state,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await tenantAdminClient.RefreshTenantConfigurationAsync(cancellationToken);
            return AdminPageOutcome.Redirect(
                successMessage: TenantSettingsMessages.RefreshSuccess,
                refreshLocalCaches: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh tenant configuration for {TenantId}", state.TenantId);
            return AdminPageOutcome.Redirect(
                errorMessage: AdminApiErrorMapper.Format(ex, TenantSettingsMessages.RefreshFailed, includeGatewayHint: true));
        }
    }

    private async Task LoadSettingsAsync(TenantSettingsWorkState state, CancellationToken cancellationToken)
    {
        try
        {
            var response = await tenantAdminClient.GetTenantSettingsAsync(state.TenantId, cancellationToken);
            state.TenantName = response.TenantName;
            state.Settings = response.Settings?.ToList() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load tenant settings for {TenantId}", state.TenantId);
            state.HasError = true;
            state.ErrorMessage = AdminApiErrorMapper.Format(ex, TenantSettingsMessages.LoadFailed, includeGatewayHint: true);
            state.Settings = [];
        }
    }

    private async Task LoadHealthAsync(TenantSettingsWorkState state, CancellationToken cancellationToken)
    {
        try
        {
            state.TenantHealth = await tenantAdminClient.GetTenantHealthAsync(state.TenantId, cancellationToken);
            state.EffectiveConfig = state.TenantHealth.EffectiveConfiguration;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load tenant health for {TenantId}", state.TenantId);
            try
            {
                state.EffectiveConfig = await tenantAdminClient.GetEffectiveConfigurationAsync(state.TenantId, cancellationToken);
            }
            catch (Exception inner)
            {
                logger.LogWarning(inner, "Failed to load effective configuration for {TenantId}", state.TenantId);
            }
        }
    }

    private async Task LoadCookbookAsync(TenantSettingsWorkState state, CancellationToken cancellationToken)
    {
        try
        {
            var response = await tenantAdminClient.GetCategoryCookbookAsync(cancellationToken);
            state.Cookbook = response.Categories?.ToList() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load category cookbook");
        }
    }

    private async Task LoadAuditLogAsync(TenantSettingsWorkState state, CancellationToken cancellationToken)
    {
        try
        {
            var log = await tenantAdminClient.GetSettingAuditLogAsync(state.TenantId, 20, cancellationToken);
            state.AuditEntries = log?.Entries?.ToList() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load audit log for {TenantId}", state.TenantId);
        }
    }
}
