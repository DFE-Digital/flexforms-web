using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Application.Dashboard;

/// <summary>
/// Loads dashboard columns and lists or creates applications for the current template.
/// </summary>
public interface IDashboardApplications
{
    Task<IReadOnlyList<DashboardColumn>> ResolveColumnsAsync(
        Guid? templateId,
        CancellationToken cancellationToken = default);

    Task<DashboardApplicationListResult> ListAsync(
        DashboardApplicationListQuery query,
        CancellationToken cancellationToken = default);

    Task<DashboardCreateApplicationResult> CreateAsync(
        Guid templateId,
        CancellationToken cancellationToken = default);
}

public sealed class DashboardApplicationsService(
    IApplicationsClient applicationsClient,
    IFormTemplateProvider formTemplateProvider,
    IContributorPatternService contributorPatternService,
    ILogger<DashboardApplicationsService> logger) : IDashboardApplications
{
    public async Task<IReadOnlyList<DashboardColumn>> ResolveColumnsAsync(
        Guid? templateId,
        CancellationToken cancellationToken = default)
    {
        if (!templateId.HasValue)
            return DashboardColumnResolver.DefaultColumns;

        try
        {
            var template = await formTemplateProvider.GetTemplateAsync(templateId.Value.ToString(), cancellationToken);
            return DashboardColumnResolver.Resolve(template);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to load dashboard columns from latest template {TemplateId}; using defaults",
                templateId);
            return DashboardColumnResolver.DefaultColumns;
        }
    }

    public async Task<DashboardApplicationListResult> ListAsync(
        DashboardApplicationListQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = query.Scope == DashboardApplicationListScope.AllForTemplate
            ? await applicationsClient.GetApplicationsByTemplateAsync(
                templateId: query.TemplateId,
                pageNumber: query.CurrentPage,
                pageSize: query.PageSize,
                applicationReference: string.IsNullOrWhiteSpace(query.SearchReference) ? null : query.SearchReference,
                dateStartedFrom: query.DateStartedFrom,
                dateStartedTo: query.DateStartedTo,
                dateSubmittedFrom: query.DateSubmittedFrom,
                dateSubmittedTo: query.DateSubmittedTo,
                status: query.Status,
                cancellationToken: cancellationToken)
            : await applicationsClient.GetMyApplicationsAsync(
                templateId: query.TemplateId,
                pageNumber: query.CurrentPage,
                pageSize: query.PageSize,
                applicationReference: string.IsNullOrWhiteSpace(query.SearchReference) ? null : query.SearchReference,
                dateStartedFrom: query.DateStartedFrom,
                dateStartedTo: query.DateStartedTo,
                dateSubmittedFrom: query.DateSubmittedFrom,
                dateSubmittedTo: query.DateSubmittedTo,
                status: query.Status,
                cancellationToken: cancellationToken);

        var totalPages = result.TotalPages;
        var currentPage = Math.Clamp(query.CurrentPage, 1, Math.Max(1, totalPages));
        var fieldColumns = query.IncludeCustomColumns
            ? query.Columns.Where(c => c.Kind == DashboardColumnKind.Field).ToList()
            : [];

        var applications = result.Items
            .Select(app =>
            {
                IReadOnlyDictionary<string, string> customValues =
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                if (fieldColumns.Count > 0)
                {
                    var formData = DashboardAnswerReader.ParseFormData(app.LatestResponse?.ResponseBody);
                    customValues = fieldColumns.ToDictionary(
                        c => c.Key,
                        c => DashboardAnswerReader.GetDisplayValue(c.FieldId!, formData),
                        StringComparer.OrdinalIgnoreCase);
                }

                return new ApplicationWithCalculatedStatus
                {
                    Application = app,
                    CalculatedStatus = DashboardApplicationStatusCalculator.GetCalculatedStatus(
                        app,
                        query.CustomStatuses,
                        logger),
                    CustomColumnValues = customValues
                };
            })
            .OrderByDescending(a => a.DateCreated)
            .ToList();

        return new DashboardApplicationListResult
        {
            Applications = applications,
            TotalPages = totalPages,
            CurrentPage = currentPage
        };
    }

    public async Task<DashboardCreateApplicationResult> CreateAsync(
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var response = await applicationsClient.CreateApplicationAsync(
            new CreateApplicationRequest
            {
                InitialResponseBody = "{}",
                TemplateId = templateId
            },
            cancellationToken);

        var contributorsEnabled = await contributorPatternService.IsEnabledAsync(
            templateId.ToString(),
            cancellationToken: cancellationToken);

        return new DashboardCreateApplicationResult
        {
            Application = response,
            ContributorsEnabled = contributorsEnabled
        };
    }
}
