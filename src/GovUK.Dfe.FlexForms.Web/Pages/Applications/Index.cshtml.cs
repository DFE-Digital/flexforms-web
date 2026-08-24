using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Application.Dashboard;
using GovUK.Dfe.FlexForms.Application.Options;
using GovUK.Dfe.FlexForms.Web.Models.Applications;
using GovUK.Dfe.FlexForms.Web.Services;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.Pages.Applications;

[Authorize(Policy = AdminAccessHelper.CanReadAnyApplicationPolicy)]
public class IndexModel(
    IDashboardApplications dashboardApplications,
    IApplicationStatusService applicationStatusService,
    IOptions<DashboardOptions> dashboardOptions,
    ILogger<IndexModel> logger) : PageModel
{
    public Guid? TemplateId { get; set; }

    public IReadOnlyList<ApplicationWithCalculatedStatus> Applications { get; private set; } = [];
    public IReadOnlyList<CustomApplicationStatusDto> CustomStatuses { get; private set; } = [];

    public int PageSize => dashboardOptions.Value.PageSize;

    public int TotalPages { get; private set; }

    public bool FiltersEnabled => dashboardOptions.Value.EnableApplicationFilters;

    public bool HasError { get; private set; }
    public string? ErrorMessage { get; private set; }

    public bool IsSearchActive => FiltersEnabled && SearchFilters.HasActiveFilters;

    public bool ShowFiltersPanel => IsSearchActive;

    [BindProperty(SupportsGet = true)]
    public IList<KeyValuePair<ApplicationStatus, string>> StatusFilters { get; set; }

    [BindProperty]
    public ApplicationStatus? SelectedStatusFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public string? SearchReference { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? DateStartedFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? DateStartedTo { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? DateSubmittedFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? DateSubmittedTo { get; set; }

    [BindProperty(SupportsGet = true)]
    public ApplicationStatus? Status { get; set; }

    public DashboardApplicationSearch SearchFilters => new()
    {
        SearchReference = SearchReference,
        DateStartedFromValue = DateStartedFrom,
        DateStartedToValue = DateStartedTo,
        DateSubmittedFromValue = DateSubmittedFrom,
        DateSubmittedToValue = DateSubmittedTo,
        Status = Status
    };

    public async Task OnGetAsync(ApplicationStatus? status = null)
    {
        var statusFilters = new List<KeyValuePair<ApplicationStatus, string>>();
        var templateId = HttpContext.Session.GetString("TemplateId");
        TemplateId = !string.IsNullOrWhiteSpace(templateId) ? Guid.Parse(templateId) : null;
        var baseApplicationStatuses = applicationStatusService.GetBaseApplicationStatuses();
        CustomStatuses = await applicationStatusService.GetCustomApplicationStatusesAsync(TemplateId);
        foreach (var item in baseApplicationStatuses)
        {
            var customStatus = CustomStatuses.FirstOrDefault(x => x.ApplicationStatus == item.Key);
            statusFilters.Add(new KeyValuePair<ApplicationStatus, string>(item.Key, customStatus?.Label != null ? customStatus.Label : item.Value));
        }
        StatusFilters = statusFilters.Where(app => AdminAccessHelper.IsAdmin(User) || AdminAccessHelper.IsSuperAdmin(User) || app.Key != ApplicationStatus.Deleted)
            .OrderBy(app => app.Key).ToList();
        SelectedStatusFilter = status;
        logger.LogInformation("TemplateId from session: {TemplateId}", TemplateId);
        ValidateSearchFilters();
        await LoadApplicationsAsync();
    }

    private void ValidateSearchFilters()
    {
        if (!FiltersEnabled)
            return;

        var filters = SearchFilters;

        if (!string.IsNullOrWhiteSpace(filters.DateStartedFromValue) && !filters.DateStartedFrom.HasValue)
            ModelState.AddModelError(nameof(DateStartedFrom), "Enter a valid date started 'from' date.");

        if (!string.IsNullOrWhiteSpace(filters.DateStartedToValue) && !filters.DateStartedTo.HasValue)
            ModelState.AddModelError(nameof(DateStartedTo), "Enter a valid date started 'to' date.");

        if (!string.IsNullOrWhiteSpace(filters.DateSubmittedFromValue) && !filters.DateSubmittedFrom.HasValue)
            ModelState.AddModelError(nameof(DateSubmittedFrom), "Enter a valid date submitted 'from' date.");

        if (!string.IsNullOrWhiteSpace(filters.DateSubmittedToValue) && !filters.DateSubmittedTo.HasValue)
            ModelState.AddModelError(nameof(DateSubmittedTo), "Enter a valid date submitted 'to' date.");

        if (filters.DateStartedFrom.HasValue && filters.DateStartedTo.HasValue && filters.DateStartedFrom > filters.DateStartedTo)
            ModelState.AddModelError(nameof(DateStartedTo), "Date started 'to' must be on or after date started 'from'.");

        if (filters.DateSubmittedFrom.HasValue && filters.DateSubmittedTo.HasValue && filters.DateSubmittedFrom > filters.DateSubmittedTo)
            ModelState.AddModelError(nameof(DateSubmittedTo), "Date submitted 'to' must be on or after date submitted 'from'.");
    }

    private async Task LoadApplicationsAsync()
    {
        if (!ModelState.IsValid)
        {
            Applications = Array.Empty<ApplicationWithCalculatedStatus>();
            return;
        }

        if (!TemplateId.HasValue)
        {
            logger.LogWarning("TemplateId not available when loading applications; rendering empty dashboard");
            Applications = Array.Empty<ApplicationWithCalculatedStatus>();
            return;
        }

        var filters = FiltersEnabled ? SearchFilters : new DashboardApplicationSearch();
        var result = await dashboardApplications.ListAsync(new DashboardApplicationListQuery
        {
            TemplateId = TemplateId.Value,
            CurrentPage = CurrentPage,
            PageSize = dashboardOptions.Value.PageSize,
            Scope = DashboardApplicationListScope.AllForTemplate,
            IncludeCustomColumns = false,
            CustomStatuses = CustomStatuses,
            SearchReference = filters.SearchReference,
            DateStartedFrom = filters.DateStartedFrom,
            DateStartedTo = filters.DateStartedTo,
            DateSubmittedFrom = filters.DateSubmittedFrom,
            DateSubmittedTo = filters.DateSubmittedTo,
            Status = filters.Status
        });

        
        Applications = result.Applications
            .Where(app => AdminAccessHelper.IsAdmin(User) || AdminAccessHelper.IsSuperAdmin(User) || app.CalculatedStatus.Key != ApplicationStatus.Deleted)
            .ToList();
        TotalPages = result.TotalPages;
        CurrentPage = result.CurrentPage;
    }
}
