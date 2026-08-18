using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.CoreLibs.Caching.Helpers;
using GovUK.Dfe.CoreLibs.Caching.Interfaces;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Security;
using GovUK.Dfe.FlexForms.Web.Services;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics.CodeAnalysis;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin
{
    [ExcludeFromCodeCoverage]
    [Authorize(Policy = AdminAccessHelper.CanAccessAdminAreaPolicy)]
    public class AdminModel(
        IAdminHome adminHome,
        ICacheService<IMemoryCacheType> cacheService,
        IHttpContextAccessor httpContextAccessor,
        IInternalUserTokenStore tokenStore,
        ITemplateSelectionService templateSelectionService,
        ITenantRequestContext tenantRequestContext,
        ILogger<AdminModel> logger)
        : PageModel
    {
        public string? TemplateId { get; set; }
        public string? TemplateName { get; set; }
        public string? TemplateDescription { get; set; }
        public int TaskGroupCount { get; set; }
        public string? CurrentTemplateVersion { get; set; }
        public string? TemplateCacheKey { get; set; }
        public bool HasError { get; set; }
        public string? ErrorMessage { get; set; }
        public bool ShowSuccess { get; set; }
        public string? SuccessMessage { get; set; }
        public string? TestToken { get; set; }
        public string? DsiToken { get; set; }
        public string? UserToken { get; set; }
        public IReadOnlyList<TemplateDto> TenantTemplates { get; private set; } = [];

        public bool IsFullAdmin => AdminAccessHelper.IsAdmin(User);

        public bool IsSuperAdmin => AdminAccessHelper.IsSuperAdmin(User);

        public bool CanManageTemplates => AdminAccessHelper.CanManageTemplates(User);

        public bool CanManageUsers => AdminAccessHelper.CanManageUsers(User);

        public bool CanManageRoles => AdminAccessHelper.CanManageRoles(User);

        public bool CanManageOrganisationSettings => AdminAccessHelper.CanManageOrganisationSettings(User);

        public bool CanManageEventMappings => AdminAccessHelper.CanManageEventMappings(User);

        public bool CanManageTenantSettings => AdminAccessHelper.CanManageTenantSettings(User);

        public bool CanViewTenantConfigurationSummary => AdminAccessHelper.CanViewTenantConfigurationSummary(User);

        /// <summary>
        /// Tenant Admin card: organisation settings, events, and own-tenant config tools.
        /// </summary>
        public bool CanAccessTenantAdminSection =>
            CanManageOrganisationSettings
            || CanManageEventMappings
            || CanManageTenantSettings
            || CanViewTenantConfigurationSummary;

        public TenantEffectiveConfigurationDto? TenantConfigurationSummary { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (TempData["AdminSuccess"] is string successMessage)
            {
                ShowSuccess = true;
                SuccessMessage = successMessage;
            }

            if (TempData["AdminError"] is string errorMessage)
            {
                HasError = true;
                ErrorMessage = errorMessage;
            }

            DsiToken = await httpContextAccessor.HttpContext?.GetTokenAsync("id_token")!;

            UserToken = tokenStore.GetToken();

            CaptureSessionTemplate();
            var state = CaptureWorkState();
            await adminHome.LoadAsync(state);
            ApplyWorkState(state);
            return Page();
        }

        public async Task<IActionResult> OnPostClearAllAsync()
        {
            if (!CanManageTemplates)
                return Forbid();

            try
            {
                HttpContext.Session.Clear();

                if (!string.IsNullOrEmpty(TemplateCacheKey))
                {
                    cacheService.Remove(TemplateCacheKey);
                    logger.LogInformation("Cleared template cache for key: {CacheKey}", TemplateCacheKey);
                }

                ShowSuccess = true;
                SuccessMessage = "Successfully cleared all sessions and caches.";

                logger.LogInformation("Admin cleared all sessions and caches");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to clear sessions and caches");
                HasError = true;
                ErrorMessage = "Failed to clear sessions and caches. Please try again.";
            }

            return RedirectToPage("/Applications/Dashboard");
        }

        public Task<IActionResult> OnPostMakeLiveAsync(Guid templateId)
            => SetTemplateLiveAsync(templateId, isLive: true);

        public Task<IActionResult> OnPostMakeNotLiveAsync(Guid templateId)
            => SetTemplateLiveAsync(templateId, isLive: false);

        private async Task<IActionResult> SetTemplateLiveAsync(Guid templateId, bool isLive)
        {
            if (!CanManageTemplates)
                return Forbid();

            var outcome = await adminHome.SetTemplateLiveAsync(templateId, isLive);

            if (outcome.SuccessMessage != null)
                TempData["AdminSuccess"] = outcome.SuccessMessage;

            if (outcome.ErrorMessage != null)
                TempData["AdminError"] = outcome.ErrorMessage;

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostOpenTemplateAsync(Guid templateId)
        {
            if (!CanManageTemplates)
                return Forbid();

            CaptureSessionTemplate();
            var state = CaptureWorkState();
            var outcome = await adminHome.OpenTemplateAsync(state, templateId);
            ApplyWorkState(state);

            if (outcome.Kind == AdminPageOutcomeKind.StayOnPage)
                return Page();

            if (state.TemplateToOpen is null)
                return Page();

            await templateSelectionService.SelectTemplateAsync(HttpContext, state.TemplateToOpen);
            return RedirectToPage("/Applications/Dashboard");
        }

        public string GetSessionKeysInfo()
        {
            var sessionKeys = new List<string>();

            var commonKeys = new[]
            {
                "TemplateId",
                "ApplicationId",
                "ApplicationReference",
                "CurrentAccumulatedApplicationId"
            };

            foreach (var key in commonKeys)
            {
                var value = HttpContext.Session.GetString(key);
                if (!string.IsNullOrEmpty(value))
                {
                    sessionKeys.Add($"{key}: {value}");
                }
            }

            return sessionKeys.Any() ? string.Join(", ", sessionKeys) : "No common session keys found";
        }

        public async Task<string> GetCacheStatusAsync()
        {
            if (string.IsNullOrEmpty(TemplateCacheKey))
            {
                return "Cache key not available";
            }

            try
            {
                var factoryCalled = false;

                await cacheService.GetOrAddAsync<FormTemplate>(
                    TemplateCacheKey,
                    async () =>
                    {
                        factoryCalled = true;
                        return null!;
                    },
                    nameof(GetCacheStatusAsync));

                return !factoryCalled ? "Template cached" : "Template not in cache";
            }
            catch
            {
                return "Unable to determine cache status";
            }
        }

        private void CaptureSessionTemplate()
        {
            TestToken = HttpContext.Session.GetString("TestAuth:Token");
            TemplateId = HttpContext.Session.GetString("TemplateId");
            if (!string.IsNullOrEmpty(TemplateId))
                TemplateCacheKey = $"FormTemplate_{CacheKeyHelper.GenerateHashedCacheKey(TemplateId)}";
        }

        private AdminHomeWorkState CaptureWorkState() =>
            new()
            {
                TenantId = tenantRequestContext.TenantId,
                IncludeTenantConfigurationSummary = CanViewTenantConfigurationSummary,
                TemplateId = TemplateId
            };

        private void ApplyWorkState(AdminHomeWorkState state)
        {
            TemplateId = state.TemplateId ?? TemplateId;
            TemplateName = state.TemplateName;
            TemplateDescription = state.TemplateDescription;
            TaskGroupCount = state.TaskGroupCount;
            CurrentTemplateVersion = state.CurrentTemplateVersion;
            TenantTemplates = state.TenantTemplates;
            TenantConfigurationSummary = state.TenantConfigurationSummary;
            if (state.HasError)
            {
                HasError = true;
                ErrorMessage = state.ErrorMessage;
            }
        }
    }
}
