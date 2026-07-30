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
        ITemplateSelectionService templateSelectionService,
        ILogger<CustomStatusLabelOverridesModel> logger)
        : PageModel
    {
        public bool ShowSuccess { get; set; }
        public bool HasError { get; set; }
        public FormTemplate? CurrentTemplate { get; set; }
        public string? CurrentVersionNumber { get; set; }
        public IReadOnlyList<TemplateDto> AvailableTemplates { get; set; } = [];

        [BindProperty(SupportsGet = true)]
        public Guid? SelectedTemplateId { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "A custom override value is required")]
        public string BaseStatusOverrideValue { get; set; } = string.Empty;

        public IEnumerable<KeyValuePair<ApplicationStatus, string>> BaseStatuses { get; set; } =
            Enumerable.Empty<KeyValuePair<ApplicationStatus, string>>();

        [BindProperty(SupportsGet = true)]
        public ApplicationStatus SelectedBaseStatus { get; set; }

        public async Task<IActionResult> OnGetAsync(
            bool success = false,
            ApplicationStatus status = ApplicationStatus.Created)
        {
            ShowSuccess = success;
            await LoadAvailableTemplatesAsync();

            var templateId = ResolveTemplateId();
            if (templateId == null)
            {
                return Page();
            }

            SelectedTemplateId = templateId.Value;
            await LoadTemplateDataAsync(templateId.Value);
            SelectedBaseStatus = status;
            BaseStatuses = applicationStatusService.GetBaseApplicationStatuses().OrderBy(x => x.Key);
            var statuses = await applicationStatusService.GetCustomApplicationStatusesAsync(templateId.Value);
            BaseStatusOverrideValue = applicationStatusService.GetStatusLabel(status, statuses);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadAvailableTemplatesAsync();

            var templateId = ResolveTemplateId();
            if (templateId == null)
            {
                ModelState.AddModelError(nameof(SelectedTemplateId), "Please select a template.");
                return Page();
            }

            SelectedTemplateId = templateId.Value;

            ApplicationStatus appStatus = ApplicationStatus.Created;
            var query = HttpContext.Request.Query;
            if (query.TryGetValue("status", out var queryStatus))
            {
                Enum.TryParse(queryStatus, out appStatus);
            }

            if (appStatus != SelectedBaseStatus)
            {
                return RedirectToPage(new { selectedTemplateId = templateId, status = SelectedBaseStatus });
            }

            if (!ValidateInput())
            {
                await LoadTemplateDataAsync(templateId.Value);
                BaseStatuses = applicationStatusService.GetBaseApplicationStatuses().OrderBy(x => x.Key);
                return Page();
            }

            await applicationStatusService.OverrideApplicationStatusLabels(templateId.Value,
                new CustomApplicationStatusRequest
                {
                    Label = BaseStatusOverrideValue,
                    ApplicationStatus = SelectedBaseStatus
                });

            logger.LogInformation("Successfully overridden application status for {TemplateId}", templateId);
            cacheService.Remove(
                $"CustomApplicationStatuses_{CacheKeyHelper.GenerateHashedCacheKey(templateId.Value.ToString())}");

            return RedirectToPage(new { selectedTemplateId = templateId, success = true, status = appStatus });
        }

        public IActionResult OnPostCancelOverride()
        {
            return new RedirectToPageResult("/Admin/Admin");
        }

        private Guid? ResolveTemplateId()
        {
            if (SelectedTemplateId.HasValue && SelectedTemplateId.Value != Guid.Empty)
            {
                return SelectedTemplateId.Value;
            }

            if (Guid.TryParse(HttpContext.Session.GetString("TemplateId"), out var sessionId)
                && sessionId != Guid.Empty)
            {
                return sessionId;
            }

            return null;
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(BaseStatusOverrideValue))
            {
                ModelState.AddModelError(nameof(BaseStatusOverrideValue),
                    "An override value is required and cannot be empty");
                return false;
            }

            return true;
        }

        private async Task LoadAvailableTemplatesAsync()
        {
            try
            {
                AvailableTemplates = await templateSelectionService.GetSelectableTemplatesAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load available templates for custom status page");
                AvailableTemplates = [];
            }
        }

        private async Task LoadTemplateDataAsync(Guid templateId)
        {
            var apiResponse = await templatesClient.GetLatestTemplateSchemaAsync(templateId);
            CurrentVersionNumber = apiResponse.VersionNumber;
            CurrentTemplate = await formTemplateProvider.GetTemplateAsync(templateId.ToString());
        }
    }
}
