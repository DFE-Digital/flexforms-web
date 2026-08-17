using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Loads tenant templates, template details, and live-status updates for the Admin home page.
/// </summary>
public interface IAdminHome
{
    Task LoadAsync(AdminHomeWorkState state, CancellationToken cancellationToken = default);

    Task<AdminPageOutcome> SetTemplateLiveAsync(
        Guid templateId,
        bool isLive,
        CancellationToken cancellationToken = default);

    Task<AdminPageOutcome> OpenTemplateAsync(
        AdminHomeWorkState state,
        Guid templateId,
        CancellationToken cancellationToken = default);
}

public sealed class AdminHomeService(
    IFormTemplateProvider templateProvider,
    ITemplatesClient templatesClient,
    ITenantAdminClient tenantAdminClient,
    ILogger<AdminHomeService> logger) : IAdminHome
{
    public async Task LoadAsync(AdminHomeWorkState state, CancellationToken cancellationToken = default)
    {
        await LoadTenantTemplatesAsync(state, cancellationToken);
        await LoadTemplateInformationAsync(state, cancellationToken);
        await LoadTenantConfigurationSummaryAsync(state, cancellationToken);
    }

    public async Task<AdminPageOutcome> SetTemplateLiveAsync(
        Guid templateId,
        bool isLive,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation(
                "Setting template {TemplateId} live status to {IsLive}",
                templateId,
                isLive);

            await templatesClient.SetTemplateLiveAsync(
                templateId,
                new SetTemplateLiveRequest { IsLive = isLive },
                cancellationToken);

            return AdminPageOutcome.Redirect(
                successMessage: isLive ? AdminHomeMessages.TemplateLive : AdminHomeMessages.TemplateNotLive);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to set live status to {IsLive} for template {TemplateId}",
                isLive,
                templateId);
            return AdminPageOutcome.Redirect(errorMessage: AdminHomeMessages.SetLiveFailed);
        }
    }

    public async Task<AdminPageOutcome> OpenTemplateAsync(
        AdminHomeWorkState state,
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await LoadTenantTemplatesAsync(state, cancellationToken);
            if (state.TenantTemplates.All(t => t.TemplateId != templateId))
            {
                state.HasError = true;
                state.ErrorMessage = AdminHomeMessages.TemplateNotInCatalogue;
                await LoadTemplateInformationAsync(state, cancellationToken);
                return AdminPageOutcome.Stay(errorMessage: AdminHomeMessages.TemplateNotInCatalogue);
            }

            state.TemplateToOpen = state.TenantTemplates.First(t => t.TemplateId == templateId);
            return AdminPageOutcome.Redirect();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to open template {TemplateId}", templateId);
            state.HasError = true;
            state.ErrorMessage = AdminHomeMessages.OpenFailed;
            await LoadTenantTemplatesAsync(state, cancellationToken);
            await LoadTemplateInformationAsync(state, cancellationToken);
            return AdminPageOutcome.Stay(errorMessage: AdminHomeMessages.OpenFailed);
        }
    }

    private async Task LoadTenantTemplatesAsync(AdminHomeWorkState state, CancellationToken cancellationToken)
    {
        try
        {
            var templates = await templatesClient.GetAccessibleTemplatesAsync(cancellationToken) ?? [];
            state.TenantTemplates = templates
                .OrderByDescending(t => t.IsLive)
                .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load tenant templates for admin page");
            state.TenantTemplates = [];
        }
    }

    private async Task LoadTemplateInformationAsync(AdminHomeWorkState state, CancellationToken cancellationToken)
    {
        try
        {
            if (state.SkipTemplateDetails || string.IsNullOrEmpty(state.TemplateId))
                return;

            var template = await templateProvider.GetTemplateAsync(state.TemplateId, cancellationToken);
            if (template != null)
            {
                state.TemplateName = template.TemplateName;
                state.TemplateDescription = template.Description;
                state.TaskGroupCount = template.TaskGroups?.Count ?? 0;
            }

            var templateResponse = await templatesClient.GetLatestTemplateSchemaAsync(
                new Guid(state.TemplateId),
                cancellationToken);
            state.CurrentTemplateVersion = templateResponse?.VersionNumber;

            logger.LogDebug("Loaded admin information for template {TemplateId}", state.TemplateId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load template information for admin page");
            state.HasError = true;
            state.ErrorMessage = AdminHomeMessages.LoadTemplateFailed;
        }
    }

    private async Task LoadTenantConfigurationSummaryAsync(
        AdminHomeWorkState state,
        CancellationToken cancellationToken)
    {
        if (!state.IncludeTenantConfigurationSummary)
            return;

        if (state.TenantId is not { } tenantId || tenantId == Guid.Empty)
            return;

        try
        {
            state.TenantConfigurationSummary = await tenantAdminClient.GetEffectiveConfigurationAsync(
                tenantId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load tenant configuration summary for admin dashboard");
        }
    }
}
