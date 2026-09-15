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
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.Pages.Applications;

[Authorize(Policy = AdminAccessHelper.CanReadAnyApplicationPolicy)]
public class IndexModel(
    IDashboardApplications dashboardApplications,
    IApplicationStatusService applicationStatusService,
    ITemplateSelectionService templateSelectionService,
    IOptions<DashboardOptions> dashboardOptions,
    ILogger<IndexModel> logger) : PageModel
{
    private Guid? _sessionTemplateId;

    public Guid? TemplateId { get; set; }

    public string? TemplateName { get; private set; }

    public IReadOnlyList<SelectListItem> TemplateOptions { get; private set; } = [];

    public bool ShowTemplateFilter => TemplateOptions.Count > 1;

    public IReadOnlyList<ApplicationWithCalculatedStatus> Applications { get; private set; } = [];
    public IReadOnlyList<CustomApplicationStatusDto> CustomStatuses { get; private set; } = [];

    public int PageSize => dashboardOptions.Value.PageSize;

    public int TotalPages { get; private set; }

    public bool FiltersEnabled => dashboardOptions.Value.EnableApplicationFilters;

    public bool HasError { get; private set; }
    public string? ErrorMessage { get; private set; }

    public bool IsSearchActive => FiltersEnabled && (SearchFilters.HasActiveFilters || IsTemplateFilterActive);

    public bool ShowFiltersPanel => IsSearchActive;

    private bool IsTemplateFilterActive =>
        SelectedTemplateId.HasValue && SelectedTemplateId != _sessionTemplateId;

    [BindProperty(SupportsGet = true)]
    public Guid? SelectedTemplateId { get; set; }

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
        await ResolveTemplateAsync();
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
        logger.LogInformation("Listing applications for template {TemplateId}", TemplateId);
        ValidateSearchFilters();
        await LoadApplicationsAsync();
    }

    /// <summary>
    /// The listing shows one template at a time. The filter overrides the template held in session
    /// for this page only, so a caseworker can look at another template without losing their selection.
    /// </summary>
    private async Task ResolveTemplateAsync()
    {
        var sessionTemplateId = templateSelectionService.GetSelectedTemplateId(HttpContext);
        _sessionTemplateId = Guid.TryParse(sessionTemplateId, out var parsed) ? parsed : null;

        var templates = await LoadSelectableTemplatesAsync();

        if (SelectedTemplateId.HasValue && templates.All(t => t.TemplateId != SelectedTemplateId.Value))
        {
            logger.LogWarning(
                "Ignoring template filter {TemplateId} because it is not accessible to the user",
                SelectedTemplateId);
            SelectedTemplateId = null;
        }

        TemplateId = SelectedTemplateId ?? _sessionTemplateId;

        TemplateName = templates.FirstOrDefault(t => t.TemplateId == TemplateId)?.Name
            ?? templateSelectionService.GetSelectedTemplateName(HttpContext);

        TemplateOptions = templates
            .Select(t => new SelectListItem(t.Name, t.TemplateId.ToString(), t.TemplateId == TemplateId))
            .ToList();
    }

    private async Task<IReadOnlyList<TemplateDto>> LoadSelectableTemplatesAsync()
    {
        try
        {
            return await templateSelectionService.GetSelectableTemplatesAsync() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load the template list for the applications filter");
            return [];
        }
    }

    /// <summary>
    /// Keeps the active filters, including the template, when moving between pages.
    /// </summary>
    public string BuildPaginationHref(int page)
    {
        var href = FiltersEnabled
            ? SearchFilters.BuildPaginationHref(page)
            : $"?currentPage={page}";

        return SelectedTemplateId.HasValue
            ? $"{href}&selectedTemplateId={SelectedTemplateId}"
            : href;
    }

    public string ClearFiltersHref =>
        SelectedTemplateId.HasValue
            ? $"/applications?selectedTemplateId={SelectedTemplateId}"
            : "/applications";

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
