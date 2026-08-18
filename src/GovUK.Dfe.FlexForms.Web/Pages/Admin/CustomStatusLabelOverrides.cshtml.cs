using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.CoreLibs.Caching.Helpers;
using GovUK.Dfe.CoreLibs.Caching.Interfaces;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
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
        ICustomStatusLabelOverridesAdmin customStatusLabelOverridesAdmin,
        ICacheService<IMemoryCacheType> cacheService) : PageModel
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
            var state = CaptureWorkState();
            await customStatusLabelOverridesAdmin.LoadAvailableTemplatesAsync(state);

            var templateId = ResolveTemplateId();
            if (templateId == null)
            {
                ApplyWorkState(state);
                return Page();
            }

            SelectedTemplateId = templateId.Value;
            state.SelectedTemplateId = templateId.Value;
            await customStatusLabelOverridesAdmin.LoadTemplateDataAsync(state, templateId.Value);
            SelectedBaseStatus = status;
            await customStatusLabelOverridesAdmin.LoadStatusOverrideAsync(state, templateId.Value, status);
            ApplyWorkState(state);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var state = CaptureWorkState();
            await customStatusLabelOverridesAdmin.LoadAvailableTemplatesAsync(state);

            var templateId = ResolveTemplateId();
            if (templateId == null)
            {
                ModelState.AddModelError(nameof(SelectedTemplateId), CustomStatusLabelOverridesMessages.SelectTemplate);
                ApplyWorkState(state);
                return Page();
            }

            SelectedTemplateId = templateId.Value;
            state.SelectedTemplateId = templateId.Value;

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
                await customStatusLabelOverridesAdmin.LoadTemplateDataAsync(state, templateId.Value);
                customStatusLabelOverridesAdmin.PopulateBaseStatuses(state);
                ApplyWorkState(state);
                return Page();
            }

            await customStatusLabelOverridesAdmin.OverrideAsync(
                templateId.Value,
                SelectedBaseStatus,
                BaseStatusOverrideValue);

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
                    CustomStatusLabelOverridesMessages.OverrideRequired);
                return false;
            }

            return true;
        }

        private CustomStatusLabelOverridesWorkState CaptureWorkState() =>
            new()
            {
                SelectedTemplateId = SelectedTemplateId,
                SelectedBaseStatus = SelectedBaseStatus,
                BaseStatusOverrideValue = BaseStatusOverrideValue
            };

        private void ApplyWorkState(CustomStatusLabelOverridesWorkState state)
        {
            SelectedTemplateId = state.SelectedTemplateId;
            CurrentTemplate = state.CurrentTemplate;
            CurrentVersionNumber = state.CurrentVersionNumber;
            AvailableTemplates = state.AvailableTemplates;
            if (state.BaseStatuses.Count > 0)
                BaseStatuses = state.BaseStatuses;
            if (!string.IsNullOrEmpty(state.BaseStatusOverrideValue))
                BaseStatusOverrideValue = state.BaseStatusOverrideValue;
        }
    }
}
