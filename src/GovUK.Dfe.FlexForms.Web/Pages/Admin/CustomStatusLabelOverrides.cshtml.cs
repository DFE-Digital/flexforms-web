using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Web.Services;
using GovUK.Dfe.CoreLibs.Caching.Helpers;
using GovUK.Dfe.CoreLibs.Caching.Interfaces;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin
{
    [ExcludeFromCodeCoverage]
    [Authorize(Policy = AdminAccessHelper.CanManageTemplatesPolicy)]
    public class CustomStatusLabelOverridesModel(
        IApplicationStatusService applicationStatusService,
        IFormTemplateProvider formTemplateProvider,
        ICacheService<IMemoryCacheType> cacheService,
        ITemplatesClient templatesClient,
        ILogger<CustomStatusLabelOverridesModel> logger)
        : PageModel
    {
        private readonly ILogger<CustomStatusLabelOverridesModel> _logger = logger;
        private readonly IApplicationStatusService _applicationStatusService = applicationStatusService;
        private readonly ICacheService<IMemoryCacheType> _cacheService = cacheService;
        private readonly IFormTemplateProvider _formTemplateProvider = formTemplateProvider;
        private readonly ITemplatesClient _templatesClient = templatesClient;

        public bool ShowSuccess { get; set; }
        public bool HasError { get; set; }
        public FormTemplate? CurrentTemplate { get; set; }
        public string? CurrentVersionNumber { get; set; }
        [BindProperty]
        [Required(ErrorMessage = "A custom override value is required")]
        public string BaseStatusOverrideValue { get; set; }
        public IEnumerable<KeyValuePair<ApplicationStatus, string>> BaseStatuses { get; set; }
        [BindProperty(SupportsGet = true)]
        public ApplicationStatus SelectedBaseStatus { get; set; }

        public async Task<IActionResult> OnGetAsync(bool success = false, ApplicationStatus status = ApplicationStatus.Created)
        {
            ShowSuccess = success;

            bool templateParsed = Guid.TryParse(HttpContext.Session.GetString("TemplateId"), out Guid templateId);

            if (!templateParsed)
            {
                _logger.LogWarning("TemplateId not found in session, or is not a valid Guid");
            }

            await LoadTemplateDataAsync(templateId);
            SelectedBaseStatus = status;
            BaseStatuses = _applicationStatusService.GetBaseApplicationStatuses().OrderBy(x => x.Key);
            var statuses = await _applicationStatusService.GetCustomApplicationStatusesAsync(templateId);
            BaseStatusOverrideValue = _applicationStatusService.GetStatusLabel(status, statuses);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            ApplicationStatus appStatus = ApplicationStatus.Created;
            var query = HttpContext.Request.Query;
            var queryHasStatus = query.TryGetValue("status", out var queryStatus);
            if(queryHasStatus)
            {
                Enum.TryParse(queryStatus, out appStatus);
            }

            if (appStatus != SelectedBaseStatus)
            {
                return RedirectToPage(new { status = SelectedBaseStatus });
            }

            bool templateParsed = Guid.TryParse(HttpContext.Session.GetString("TemplateId"), out Guid templateId);
            if (!templateParsed)
            {
                _logger.LogWarning("TemplateId not found in session during post.");
                return RedirectToPage("/Applications/Dashboard");
            }

            if (!ValidateInput())
            {
                await LoadTemplateDataAsync(templateId);
                BaseStatuses = _applicationStatusService.GetBaseApplicationStatuses().OrderBy(x => x.Key);
                return Page();
            }

            await _applicationStatusService.OverrideApplicationStatusLabels(templateId,
                new CustomApplicationStatusRequest
                {
                    Label = BaseStatusOverrideValue,
                    ApplicationStatus = SelectedBaseStatus
                });
            
            _logger.LogInformation("Successfully overriden submitted application status for {TemplateId}", templateId);
            _cacheService.Remove($"CustomApplicationStatuses_{CacheKeyHelper.GenerateHashedCacheKey(templateId.ToString())}");

            return RedirectToPage(new { success = true, status = appStatus });
        }

        private bool ValidateInput()
        {
            var isValid = true;

            if (string.IsNullOrWhiteSpace(BaseStatusOverrideValue))
            {
                ModelState.AddModelError(nameof(BaseStatusOverrideValue), "An override value for \"In Progress\" is required and cannot be empty");
                isValid = false;
            }

            return isValid;
        }

        public IActionResult OnPostCancelOverride()
        {
            return new RedirectToPageResult("/Admin/Admin");
        }

        private async Task LoadTemplateDataAsync(Guid templateId)
        {
            var apiResponse = await _templatesClient.GetLatestTemplateSchemaAsync(templateId);
            CurrentVersionNumber = apiResponse.VersionNumber;
            CurrentTemplate = await _formTemplateProvider.GetTemplateAsync(templateId.ToString());
        }
    }
}
