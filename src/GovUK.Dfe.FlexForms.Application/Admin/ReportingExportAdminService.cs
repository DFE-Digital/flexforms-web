using System.Text;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Lets tenant admins choose which answers are exported to reporting: the default for fields nobody has decided
/// about (tenant-wide or per template) and Allow/Deny decisions per field.
/// </summary>
public interface IReportingExportAdmin
{
    Task LoadAsync(ReportingExportWorkState state, CancellationToken cancellationToken = default);

    Task<AdminPageOutcome> SetTenantDefaultAsync(
        ReportingExportWorkState state,
        ReportingExportModeSetting mode,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<AdminPageOutcome> SetTemplateDefaultAsync(
        ReportingExportWorkState state,
        ReportingExportModeSetting mode,
        string? reason,
        CancellationToken cancellationToken = default);

    /// <param name="fieldKeys">Values produced by <see cref="ReportingExportAdminService.FieldKey"/>.</param>
    Task<AdminPageOutcome> DecideAsync(
        ReportingExportWorkState state,
        IReadOnlyCollection<string> fieldKeys,
        ReportingExportDecision decision,
        string? reason,
        CancellationToken cancellationToken = default);
}

public sealed class ReportingExportAdminService(
    IReportingExportClient reportingExportClient,
    ITemplatesClient templatesClient,
    ILogger<ReportingExportAdminService> logger) : IReportingExportAdmin
{
    public const string SelectedTemplateRouteKey = "selectedTemplateId";
    public const string RefreshRouteKey = "refreshId";

    public async Task LoadAsync(ReportingExportWorkState state, CancellationToken cancellationToken = default)
    {
        await LoadTemplatesAsync(state, cancellationToken);

        try
        {
            state.TenantDefault = await reportingExportClient.GetTenantReportingExportDefaultAsync(state.TenantId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load the reporting export default for tenant {TenantId}", state.TenantId);
            Fail(state, AdminApiErrorMapper.Format(ex, ReportingExportMessages.LoadDefaultFailed));
            return;
        }

        if (state.RefreshId is { } refreshId)
        {
            try
            {
                state.Refresh = await reportingExportClient.GetReportingExportRefreshAsync(state.TenantId, refreshId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load reporting refresh {RefreshId}", refreshId);
                Fail(state, AdminApiErrorMapper.Format(ex, ReportingExportMessages.LoadRefreshFailed));
            }
        }

        if (state.SelectedTemplateId is not { } templateId)
        {
            return;
        }

        try
        {
            state.TemplateDefault = await reportingExportClient.GetTemplateReportingExportDefaultAsync(state.TenantId, templateId, cancellationToken);
            state.Policy = await reportingExportClient.GetTemplateReportingExportPolicyAsync(state.TenantId, templateId, cancellationToken);
        }
        catch (ExternalApplicationsException ex) when (ex.StatusCode == 404)
        {
            state.PolicyMessage = AdminApiErrorMapper.Format(ex, ReportingExportMessages.LoadPolicyFailed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load the reporting export policy for template {TemplateId}", templateId);
            Fail(state, AdminApiErrorMapper.Format(ex, ReportingExportMessages.LoadPolicyFailed));
        }
    }

    public Task<AdminPageOutcome> SetTenantDefaultAsync(
        ReportingExportWorkState state,
        ReportingExportModeSetting mode,
        string? reason,
        CancellationToken cancellationToken = default) =>
        SetDefaultAsync(state, null, mode, reason, cancellationToken);

    public Task<AdminPageOutcome> SetTemplateDefaultAsync(
        ReportingExportWorkState state,
        ReportingExportModeSetting mode,
        string? reason,
        CancellationToken cancellationToken = default) =>
        state.SelectedTemplateId is { } templateId
            ? SetDefaultAsync(state, templateId, mode, reason, cancellationToken)
            : Task.FromResult(AdminPageOutcome.Redirect(errorMessage: ReportingExportMessages.SelectTemplate));

    public async Task<AdminPageOutcome> DecideAsync(
        ReportingExportWorkState state,
        IReadOnlyCollection<string> fieldKeys,
        ReportingExportDecision decision,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        if (state.SelectedTemplateId is not { } templateId)
        {
            return AdminPageOutcome.Redirect(errorMessage: ReportingExportMessages.SelectTemplate);
        }

        var fields = fieldKeys.Select(ParseFieldKey).OfType<(string ParentFieldId, string FieldId)>().Distinct().ToList();
        if (fields.Count == 0)
        {
            return AdminPageOutcome.Redirect(errorMessage: ReportingExportMessages.SelectFields, routeValues: RouteValues(state, null));
        }

        var request = new UpdateReportingExportDecisionsRequest
        {
            Decisions = fields.Select(f => new ReportingExportDecisionRequest
            {
                ParentFieldId = f.ParentFieldId,
                FieldId = f.FieldId,
                Decision = decision,
                Reason = Normalise(reason),
            }).ToList(),
        };

        try
        {
            var result = await reportingExportClient.UpdateTemplateReportingExportDecisionsAsync(state.TenantId, templateId, request, cancellationToken);
            return Changed(state, result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to change reporting export decisions for template {TemplateId}", templateId);
            return AdminPageOutcome.Redirect(
                errorMessage: AdminApiErrorMapper.Format(ex, ReportingExportMessages.SaveFailed),
                routeValues: RouteValues(state, null));
        }
    }

    /// <summary>A form value identifying one field of a template, safe for any characters in its ids.</summary>
    public static string FieldKey(string? parentFieldId, string fieldId) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{parentFieldId}\n{fieldId}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal static (string ParentFieldId, string FieldId)? ParseFieldKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        try
        {
            var base64 = key.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(base64)).Split('\n', 2);
            return parts.Length == 2 && parts[1].Length > 0 ? (parts[0], parts[1]) : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private async Task<AdminPageOutcome> SetDefaultAsync(
        ReportingExportWorkState state,
        Guid? templateId,
        ReportingExportModeSetting mode,
        string? reason,
        CancellationToken cancellationToken)
    {
        reason = Normalise(reason);
        if (mode == ReportingExportModeSetting.ExportAll && reason is null)
        {
            return AdminPageOutcome.Redirect(errorMessage: ReportingExportMessages.ReasonRequiredForExportAll, routeValues: RouteValues(state, null));
        }

        var request = new UpdateReportingExportDefaultRequest { Mode = mode, Reason = reason };
        try
        {
            var result = templateId is { } id
                ? await reportingExportClient.UpdateTemplateReportingExportDefaultAsync(state.TenantId, id, request, cancellationToken)
                : await reportingExportClient.UpdateTenantReportingExportDefaultAsync(state.TenantId, request, cancellationToken);
            return Changed(state, result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to change the reporting export default for tenant {TenantId}, template {TemplateId}", state.TenantId, templateId);
            return AdminPageOutcome.Redirect(
                errorMessage: AdminApiErrorMapper.Format(ex, ReportingExportMessages.SaveFailed),
                routeValues: RouteValues(state, null));
        }
    }

    private async Task LoadTemplatesAsync(ReportingExportWorkState state, CancellationToken cancellationToken)
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
            logger.LogWarning(ex, "Failed to load templates for the reporting export page");
            state.AvailableTemplates = [];
        }
    }

    private static AdminPageOutcome Changed(ReportingExportWorkState state, ReportingExportChangeResultDto result)
    {
        var message = result.Status == ReportingExportChangeStatus.Unchanged ? ReportingExportMessages.Unchanged : ReportingExportMessages.Saved;
        if (result.Warnings is { Count: > 0 } warnings)
        {
            message = $"{message} {string.Join(" ", warnings)}";
        }

        return AdminPageOutcome.Redirect(successMessage: message, routeValues: RouteValues(state, result.RefreshId));
    }

    private static Dictionary<string, string?> RouteValues(ReportingExportWorkState state, Guid? refreshId)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (state.SelectedTemplateId is { } templateId)
        {
            values[SelectedTemplateRouteKey] = templateId.ToString();
        }

        if (refreshId is { } id)
        {
            values[RefreshRouteKey] = id.ToString();
        }

        return values;
    }

    private static string? Normalise(string? reason) => string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

    private static void Fail(ReportingExportWorkState state, string message)
    {
        state.HasError = true;
        state.ErrorMessage = message;
    }
}
