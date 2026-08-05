using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.CoreLibs.Caching.Helpers;
using GovUK.Dfe.CoreLibs.Caching.Interfaces;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Web.Services;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Authentication;
using Task = System.Threading.Tasks.Task;
using GovUK.Dfe.FlexForms.Api.Client.Security;
using GovUK.Dfe.FlexForms.Web.Tenancy;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin
{
    [ExcludeFromCodeCoverage]
    [Authorize(Policy = AdminAccessHelper.CanAccessAdminAreaPolicy)]
    public class AdminModel(
        IFormTemplateProvider templateProvider,
        ITemplatesClient templatesClient,
        ITemplateSelectionService templateSelectionService,
        ICacheService<IMemoryCacheType> cacheService,
        IHttpContextAccessor httpContextAccessor,
        IInternalUserTokenStore tokenStore,
        ITenantAdminClient tenantAdminClient,
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

        public bool CanManageTemplates => AdminAccessHelper.CanManageTemplates(User);

        public bool CanManageUsers => AdminAccessHelper.CanManageUsers(User);

        public bool CanManageRoles => AdminAccessHelper.CanManageRoles(User);

        public bool CanManageOrganisationSettings => AdminAccessHelper.CanManageOrganisationSettings(User);

        public bool CanManageTenantSettings => AdminAccessHelper.CanManageTenantSettings(User);

        public bool CanViewTenantConfigurationSummary => AdminAccessHelper.CanViewTenantConfigurationSummary(User);

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

            await LoadTenantTemplatesAsync();
            await LoadTemplateInformationAsync();
            await LoadTenantConfigurationSummaryAsync();
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

            await LoadTemplateInformationAsync(true);
            return RedirectToPage("/Applications/Dashboard");
        }

        public IActionResult OnPostGoToTemplateManager()
        {
            if (!CanManageTemplates)
                return Forbid();

            return RedirectToPage("/Admin/TemplateManager");
        }

        public IActionResult OnPostGoToCustomStatusLabelOverrides()
        {
            if (!CanManageTemplates)
                return Forbid();

            return RedirectToPage("/Admin/CustomStatusLabelOverrides");
        }

        public Task<IActionResult> OnPostMakeLiveAsync(Guid templateId)
            => SetTemplateLiveAsync(templateId, isLive: true);

        public Task<IActionResult> OnPostMakeNotLiveAsync(Guid templateId)
            => SetTemplateLiveAsync(templateId, isLive: false);

        private async Task<IActionResult> SetTemplateLiveAsync(Guid templateId, bool isLive)
        {
            if (!CanManageTemplates)
                return Forbid();

            try
            {
                logger.LogInformation(
                    "Setting template {TemplateId} live status to {IsLive}",
                    templateId,
                    isLive);

                await templatesClient.SetTemplateLiveAsync(
                    templateId,
                    new SetTemplateLiveRequest { IsLive = isLive });

                TempData["AdminSuccess"] = isLive
                    ? "Template is now live for end users."
                    : "Template is no longer live for end users.";
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to set live status to {IsLive} for template {TemplateId}",
                    isLive,
                    templateId);
                TempData["AdminError"] = "Failed to update template live status. Please try again.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostOpenTemplateAsync(Guid templateId)
        {
            if (!CanManageTemplates)
                return Forbid();

            try
            {
                var templates = await templateSelectionService.GetSelectableTemplatesAsync();
                if (templates.All(t => t.TemplateId != templateId))
                {
                    HasError = true;
                    ErrorMessage = "Template was not found in the tenant catalogue.";
                    await LoadTenantTemplatesAsync();
                    await LoadTemplateInformationAsync();
                    return Page();
                }

                var template = templates.First(t => t.TemplateId == templateId);
                await templateSelectionService.SelectTemplateAsync(HttpContext, template);
                return RedirectToPage("/Applications/Dashboard");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to open template {TemplateId}", templateId);
                HasError = true;
                ErrorMessage = "Failed to open template. Please try again.";
                await LoadTenantTemplatesAsync();
                await LoadTemplateInformationAsync();
                return Page();
            }
        }

        private async Task LoadTenantTemplatesAsync()
        {
            try
            {
                TenantTemplates = await templateSelectionService.GetSelectableTemplatesAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load tenant templates for admin page");
                TenantTemplates = [];
            }
        }

        private async Task LoadTemplateInformationAsync(bool afterSessionClear = false)
        {
            try
            {
                TestToken = HttpContext.Session.GetString("TestAuth:Token");

                TemplateId = HttpContext.Session.GetString("TemplateId");

                if (afterSessionClear)
                    return;

                if (string.IsNullOrEmpty(TemplateId))
                {
                    return;
                }

                TemplateCacheKey = $"FormTemplate_{CacheKeyHelper.GenerateHashedCacheKey(TemplateId)}";

                var template = await templateProvider.GetTemplateAsync(TemplateId);
                if (template != null)
                {
                    TemplateName = template.TemplateName;
                    TemplateDescription = template.Description;
                    TaskGroupCount = template.TaskGroups?.Count ?? 0;
                }

                var templateResponse = await templatesClient.GetLatestTemplateSchemaAsync(new Guid(TemplateId));
                CurrentTemplateVersion = templateResponse?.VersionNumber;

                logger.LogDebug("Loaded admin information for template {TemplateId}", TemplateId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load template information for admin page");
                HasError = true;
                ErrorMessage = "Failed to load template information. Please try again.";
            }
        }

        private async Task LoadTenantConfigurationSummaryAsync()
        {
            if (!CanViewTenantConfigurationSummary)
            {
                return;
            }

            if (tenantRequestContext.TenantId is not { } tenantId || tenantId == Guid.Empty)
            {
                return;
            }

            try
            {
                TenantConfigurationSummary = await tenantAdminClient.GetEffectiveConfigurationAsync(tenantId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load tenant configuration summary for admin dashboard");
            }
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
    }
}
