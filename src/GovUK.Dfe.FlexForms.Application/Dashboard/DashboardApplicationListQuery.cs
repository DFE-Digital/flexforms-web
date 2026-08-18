using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;

namespace GovUK.Dfe.FlexForms.Application.Dashboard;

public enum DashboardApplicationListScope
{
    Mine,
    AllForTemplate
}

/// <summary>
/// Query for listing applications on the dashboard or admin index.
/// </summary>
public sealed class DashboardApplicationListQuery
{
    public Guid TemplateId { get; init; }

    public int CurrentPage { get; init; } = 1;

    public int PageSize { get; init; } = 50;

    public DashboardApplicationListScope Scope { get; init; } = DashboardApplicationListScope.Mine;

    public bool IncludeCustomColumns { get; init; }

    public IReadOnlyList<DashboardColumn> Columns { get; init; } = DashboardColumnResolver.DefaultColumns;

    public IReadOnlyList<CustomApplicationStatusDto> CustomStatuses { get; init; } = [];

    public string? SearchReference { get; init; }

    public DateTime? DateStartedFrom { get; init; }

    public DateTime? DateStartedTo { get; init; }

    public DateTime? DateSubmittedFrom { get; init; }

    public DateTime? DateSubmittedTo { get; init; }

    public ApplicationStatus? Status { get; init; }
}

/// <summary>
/// Result of a dashboard application list query.
/// </summary>
public sealed class DashboardApplicationListResult
{
    public IReadOnlyList<ApplicationWithCalculatedStatus> Applications { get; init; } = [];

    public int TotalPages { get; init; }

    public int CurrentPage { get; init; } = 1;
}

/// <summary>
/// Result of creating an application from the dashboard.
/// </summary>
public sealed class DashboardCreateApplicationResult
{
    public ApplicationDto Application { get; init; } = null!;

    public bool ContributorsEnabled { get; init; }
}
