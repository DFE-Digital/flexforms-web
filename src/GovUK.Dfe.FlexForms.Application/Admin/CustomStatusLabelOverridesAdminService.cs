using System.ComponentModel;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Loads templates and overrides application status labels.
/// </summary>
public interface ICustomStatusLabelOverridesAdmin
{
    Task LoadAvailableTemplatesAsync(
        CustomStatusLabelOverridesWorkState state,
        CancellationToken cancellationToken = default);

    Task LoadTemplateDataAsync(
        CustomStatusLabelOverridesWorkState state,
        Guid templateId,
        CancellationToken cancellationToken = default);

    Task LoadStatusOverrideAsync(
        CustomStatusLabelOverridesWorkState state,
        Guid templateId,
        ApplicationStatus status,
        CancellationToken cancellationToken = default);

    Task OverrideAsync(
        Guid templateId,
        ApplicationStatus status,
        string label,
        CancellationToken cancellationToken = default);

    void PopulateBaseStatuses(CustomStatusLabelOverridesWorkState state);
}

public sealed class CustomStatusLabelOverridesAdminService(
    IFormTemplateProvider formTemplateProvider,
    ITemplatesClient templatesClient,
    ILogger<CustomStatusLabelOverridesAdminService> logger) : ICustomStatusLabelOverridesAdmin
{
    public async Task LoadAvailableTemplatesAsync(
        CustomStatusLabelOverridesWorkState state,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var templates = await templatesClient.GetAccessibleTemplatesAsync(cancellationToken) ?? [];
            state.AvailableTemplates = templates
                .OrderByDescending(t => t.IsLive)
                .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load available templates for custom status page");
            state.AvailableTemplates = [];
        }
    }

    public async Task LoadTemplateDataAsync(
        CustomStatusLabelOverridesWorkState state,
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var apiResponse = await templatesClient.GetLatestTemplateSchemaAsync(templateId, cancellationToken);
        state.CurrentVersionNumber = apiResponse.VersionNumber;
        state.CurrentTemplate = await formTemplateProvider.GetTemplateAsync(templateId.ToString(), cancellationToken);
    }

    public async Task LoadStatusOverrideAsync(
        CustomStatusLabelOverridesWorkState state,
        Guid templateId,
        ApplicationStatus status,
        CancellationToken cancellationToken = default)
    {
        state.BaseStatuses = GetBaseApplicationStatuses().OrderBy(x => x.Key).ToList();
        var statuses = await templatesClient.GetCustomApplicationStatusesAsync(templateId, cancellationToken);
        state.BaseStatusOverrideValue = GetStatusLabel(status, statuses);
    }

    public async Task OverrideAsync(
        Guid templateId,
        ApplicationStatus status,
        string label,
        CancellationToken cancellationToken = default)
    {
        await templatesClient.CreateCustomApplicationStatusAsync(
            templateId,
            new CustomApplicationStatusRequest
            {
                Label = label,
                ApplicationStatus = status
            },
            cancellationToken);

        logger.LogInformation("Successfully overridden application status for {TemplateId}", templateId);
    }

    public void PopulateBaseStatuses(CustomStatusLabelOverridesWorkState state)
    {
        state.BaseStatuses = GetBaseApplicationStatuses().OrderBy(x => x.Key).ToList();
    }

    internal static List<KeyValuePair<ApplicationStatus, string>> GetBaseApplicationStatuses()
    {
        var baseStatuses = new List<KeyValuePair<ApplicationStatus, string>>();
        foreach (var status in Enum.GetValues<ApplicationStatus>())
        {
            baseStatuses.Add(new KeyValuePair<ApplicationStatus, string>(status, GetBaseStatusLabel(status)));
        }

        return baseStatuses;
    }

    internal static string GetStatusLabel(
        ApplicationStatus status,
        IEnumerable<GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response.CustomApplicationStatusDto>? customStatuses)
    {
        if (customStatuses != null)
        {
            var customStatus = customStatuses.FirstOrDefault(x => x.ApplicationStatus == status);
            if (customStatus?.Label != null)
                return customStatus.Label;
        }

        return GetBaseStatusLabel(status);
    }

    private static string GetBaseStatusLabel(ApplicationStatus status)
    {
        var appStatusInfo = status.GetType().GetField(status.ToString());
        var descriptionAttributes = (DescriptionAttribute[])appStatusInfo!.GetCustomAttributes(typeof(DescriptionAttribute), false);
        return descriptionAttributes.Length > 0 ? descriptionAttributes[0].Description : status.ToString();
    }
}
