using GovUK.Dfe.FlexForms.Application.Dashboard;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Options;
using GovUK.Dfe.FlexForms.Web.Models.Applications;
using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Web.Services;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Json;
using SystemTask = System.Threading.Tasks.Task;
using StackExchange.Redis;

namespace GovUK.Dfe.FlexForms.Web.Pages.Applications
{
    [ExcludeFromCodeCoverage]
    [Authorize]
    public class DashboardModel(
        ILogger<DashboardModel> logger,
        IApplicationStatusService applicationStatusService,
        IApplicationsClient applicationsClient,
        IUsersClient usersClient,
        IHttpContextAccessor httpContextAccessor,
        IApplicationResponseService applicationResponseService,
        IContributorPatternService contributorPatternService,
        IFormTemplateProvider formTemplateProvider,
        IMemoryCache memoryCache,
        IOptions<DashboardOptions> dashboardOptions)
        : PageModel
    {
        public string? Email { get; private set; }
        public string? FirstName { get; private set; }
        public string? LastName { get; private set; }
        public string? OrganisationName { get; private set; }
        public IReadOnlyList<ApplicationWithCalculatedStatus> Applications { get; private set; } = Array.Empty<ApplicationWithCalculatedStatus>();
        public IReadOnlyList<CustomApplicationStatusDto> CustomStatuses { get; private set; } = [];
        public IReadOnlyList<DashboardColumn> Columns { get; private set; } = DashboardColumnResolver.DefaultColumns;
        public bool HasError { get; private set; }
        public string? ErrorMessage { get; private set; }

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

        [BindProperty(SupportsGet = true)]
        public IList<KeyValuePair<ApplicationStatus, string>> StatusFilters { get; set; }

        [BindProperty]
        public ApplicationStatus? SelectedStatusFilter { get; set; }

        public DashboardApplicationSearch SearchFilters => new()
        {
            SearchReference = SearchReference,
            DateStartedFromValue = DateStartedFrom,
            DateStartedToValue = DateStartedTo,
            DateSubmittedFromValue = DateSubmittedFrom,
            DateSubmittedToValue = DateSubmittedTo,
            Status = Status
        };

        public int TotalPages { get; private set; }
        public int PageSize => dashboardOptions.Value.PageSize;
        public bool FiltersEnabled => dashboardOptions.Value.EnableApplicationFilters;
        public bool IsSearchActive => FiltersEnabled && SearchFilters.HasActiveFilters;
        public bool ShowFiltersPanel => IsSearchActive;

        public class ApplicationWithCalculatedStatus
        {
            public ApplicationDto Application { get; set; } = null!;
            public KeyValuePair<ApplicationStatus, string> CalculatedStatus { get; set; }
            public IReadOnlyDictionary<string, string> CustomColumnValues { get; set; } =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public Guid ApplicationId => Application.ApplicationId;
            public string ApplicationReference => Application.ApplicationReference;
            public string TemplateName => Application.TemplateName;
            public DateTime DateCreated => Application.DateCreated;
            public DateTime? DateSubmitted => Application.DateSubmitted;
        }

        public async SystemTask OnGetAsync(ApplicationStatus? status = null)
        {
            var statusFilters = new List<KeyValuePair<ApplicationStatus, string>>();
            var baseApplicationStatuses = applicationStatusService.GetBaseApplicationStatuses();
            CustomStatuses = await applicationStatusService.GetCustomApplicationStatusesAsync(ResolveTemplateId());
            foreach(var item in baseApplicationStatuses)
            {
                var customStatus = CustomStatuses.FirstOrDefault(x => x.ApplicationStatus == item.Key);
                statusFilters.Add(new KeyValuePair<ApplicationStatus, string>(item.Key, customStatus?.Label != null ? customStatus.Label : item.Value));
            }
            StatusFilters = statusFilters.Where(app => AdminAccessHelper.IsAdmin(User) || AdminAccessHelper.IsSuperAdmin(User) || app.Key != ApplicationStatus.Deleted)
                .OrderBy(app => app.Key).ToList();
            SelectedStatusFilter = status;
            ValidateSearchFilters();
            await LoadDashboardColumnsAsync();
            await LoadUserDetailsAsync();
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

        public string BuildPaginationHref(int page) =>
            FiltersEnabled
                ? SearchFilters.BuildPaginationHref(page)
                : $"?currentPage={page}";

        public async Task<IActionResult> OnPostCreateApplicationAsync()
        {
            var templateGuid = ResolveTemplateId();
            if (!templateGuid.HasValue)
            {
                HasError = true;
                ErrorMessage = "Template is not configured. Please refresh the page.";
                logger.LogWarning("TemplateId not available when creating application");
                return Page();
            }

            var response = await applicationsClient.CreateApplicationAsync(new CreateApplicationRequest
            {
                InitialResponseBody = "{}",
                TemplateId = templateGuid.Value
            });

            HttpContext.Session.SetString("ApplicationId", response.ApplicationId.ToString());
            HttpContext.Session.SetString("ApplicationReference", response.ApplicationReference);
            HttpContext.Session.SetString($"ApplicationStatus_{response.ApplicationId}", response.Status?.ToString() ?? ApplicationStatus.InProgress.ToString());

            var currentUserEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
            var currentUserName = User.FindFirstValue(ClaimTypes.Name) ?? currentUserEmail ?? string.Empty;
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(currentUserEmail))
            {
                HttpContext.Session.SetString($"ApplicationLeadApplicantEmail_{response.ApplicationId}", currentUserEmail);
            }
            if (!string.IsNullOrEmpty(currentUserName))
            {
                HttpContext.Session.SetString($"ApplicationLeadApplicantName_{response.ApplicationId}", currentUserName);
            }
            if (!string.IsNullOrEmpty(currentUserId))
            {
                HttpContext.Session.SetString($"ApplicationLeadApplicantUserId_{response.ApplicationId}", currentUserId);
            }

            // Clear any existing accumulated form data when starting a new application
            applicationResponseService.ClearAccumulatedFormData(HttpContext.Session);
            HttpContext.Session.SetString("CurrentAccumulatedApplicationId", response.ApplicationId.ToString());

            if (User.Identity?.IsAuthenticated == true)
            {
                await UserPermissionsCache.RefreshAsync(memoryCache, usersClient, User, logger, CancellationToken.None);
                UserPermissionsCache.RemovePermissionClaims(User);
            }

            logger.LogInformation("Created new application {ApplicationId} and cleared accumulated form data", response.ApplicationId);

            // Note: Token management now handled automatically by TokenManagementMiddleware
            var templateId = templateGuid.Value.ToString();
            if (await contributorPatternService.IsEnabledAsync(templateId))
            {
                return RedirectToPage("/Applications/Contributors", new { referenceNumber = response.ApplicationReference });
            }

            return RedirectToPage("/FormEngine/RenderForm", new { referenceNumber = response.ApplicationReference });
        }

        private async SystemTask LoadDashboardColumnsAsync()
        {
            Columns = DashboardColumnResolver.DefaultColumns;

            var templateGuid = ResolveTemplateId();
            if (!templateGuid.HasValue)
                return;

            try
            {
                var template = await formTemplateProvider.GetTemplateAsync(templateGuid.Value.ToString());
                Columns = DashboardColumnResolver.Resolve(template);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load dashboard columns from latest template {TemplateId}; using defaults", templateGuid);
                Columns = DashboardColumnResolver.DefaultColumns;
            }
        }

        private async SystemTask LoadApplicationsAsync()
        {
            if (!ModelState.IsValid)
            {
                Applications = Array.Empty<ApplicationWithCalculatedStatus>();
                return;
            }

            var templateGuid = ResolveTemplateId();
            if (!templateGuid.HasValue)
            {
                // Try again on next request; show empty state instead of erroring
                logger.LogWarning("TemplateId not available when loading applications; rendering empty dashboard");
                Applications = Array.Empty<ApplicationWithCalculatedStatus>();
                return;
            }

            var pageSize = dashboardOptions.Value.PageSize;
            var filters = FiltersEnabled ? SearchFilters : new DashboardApplicationSearch();
            var result = await applicationsClient.GetMyApplicationsAsync(
                templateId: templateGuid.Value,
                pageNumber: CurrentPage,
                pageSize: pageSize,
                applicationReference: string.IsNullOrWhiteSpace(filters.SearchReference) ? null : filters.SearchReference,
                dateStartedFrom: filters.DateStartedFrom,
                dateStartedTo: filters.DateStartedTo,
                dateSubmittedFrom: filters.DateSubmittedFrom,
                dateSubmittedTo: filters.DateSubmittedTo,
                status: filters.Status);

            TotalPages = result.TotalPages;
            CurrentPage = Math.Clamp(CurrentPage, 1, Math.Max(1, TotalPages));

            var fieldColumns = Columns.Where(c => c.Kind == DashboardColumnKind.Field).ToList();

            var applicationTasks = result.Items.Where(app => AdminAccessHelper.IsAdmin(User) || AdminAccessHelper.IsSuperAdmin(User) || app.Status != ApplicationStatus.Deleted)
                .AsEnumerable().Select(async app =>
            {
                var formData = DashboardAnswerReader.ParseFormData(app.LatestResponse?.ResponseBody);
                var customValues = fieldColumns.ToDictionary(
                    c => c.Key,
                    c => DashboardAnswerReader.GetDisplayValue(c.FieldId!, formData),
                    StringComparer.OrdinalIgnoreCase);

                return new ApplicationWithCalculatedStatus
                {
                    Application = app,
                    CalculatedStatus = applicationStatusService.GetCalculatedApplicationStatusAsync(app, CustomStatuses),
                    CustomColumnValues = customValues
                };
            });

            Applications = [..(await SystemTask.WhenAll(applicationTasks))
                .OrderByDescending(a => a.DateCreated)];
        }

        private Guid? ResolveTemplateId()
        {
            try
            {
                var templateId = HttpContext.Session.GetString("TemplateId");
                if (Guid.TryParse(templateId, out var guid))
                {
                    return guid;
                }

                // Fallback to configuration
                var configuration = HttpContext.RequestServices.GetService(typeof(IRequestAppConfiguration)) as IRequestAppConfiguration;
                var configured = configuration?["Template:Id"];
                if (Guid.TryParse(configured, out var cfgGuid))
                {
                    // Persist into session for subsequent requests
                    HttpContext.Session.SetString("TemplateId", cfgGuid.ToString());
                    return cfgGuid;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to resolve TemplateId");
            }

            return null;
        }

        private SystemTask LoadUserDetailsAsync()
        {
            Email = User.FindFirst(ClaimTypes.Email)?.Value
                    ?? User.FindFirst("email")?.Value;

            FirstName = User.FindFirst(ClaimTypes.GivenName)?.Value;
            LastName = User.FindFirst(ClaimTypes.Surname)?.Value;

            var orgJson = User.FindFirst("organisation")?.Value;
            if (!string.IsNullOrEmpty(orgJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(orgJson);
                    OrganisationName = doc.RootElement
                        .GetProperty("name")
                        .GetString();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse organisation JSON for user {Email}", Email);
                    OrganisationName = null;
                }
            }

            return SystemTask.CompletedTask;
        }
    }
}
