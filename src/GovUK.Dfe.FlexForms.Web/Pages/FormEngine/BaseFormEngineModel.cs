using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Web.Pages.Shared;
using GovUK.Dfe.FlexForms.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Web.Pages.FormEngine
{
    /// <summary>
    /// Base class for form engine page models containing common functionality
    /// </summary>
    public abstract class BaseFormEngineModel(
        IFieldRendererService renderer,
        IApplicationResponseService applicationResponseService,
        IFieldFormattingService fieldFormattingService,
        ITemplateManagementService templateManagementService,
        IApplicationStateService applicationStateService,
        IFormStateManager formStateManager,
        IFormNavigationService formNavigationService,
        ILogger logger)
        : BaseFormPageModel(renderer, applicationResponseService, fieldFormattingService, templateManagementService,
            applicationStateService, logger)
    {
        protected readonly IFormStateManager _formStateManager = formStateManager;
        protected readonly IFormNavigationService _formNavigationService = formNavigationService;

        public FormState CurrentFormState { get; set; }
        public TaskGroup CurrentGroup { get; set; }
        public Domain.Models.Task CurrentTask { get; set; }
        public Domain.Models.Page CurrentPage { get; set; }

        [BindProperty(SupportsGet = true, Name = "taskId")] public string TaskId { get; set; } = string.Empty;
        [BindProperty(SupportsGet = true, Name = "pageId")] public string CurrentPageId { get; set; } = string.Empty;
        [BindProperty(SupportsGet = true, Name = "flowId")] public string? FlowId { get; set; }
        [BindProperty(SupportsGet = true, Name = "instanceId")] public string? InstanceId { get; set; }

        protected FormState GetCurrentFormState() =>
            _formStateManager.GetCurrentState(ReferenceNumber, TaskId, CurrentPageId);

        /// <summary>
        /// Ensures <see cref="CurrentFormState"/> is populated when returning <see cref="PageResult"/> from an exception filter.
        /// </summary>
        public void EnsureFormStateForErrorDisplay()
        {
            if (CurrentFormState == default)
                CurrentFormState = GetCurrentFormState();
        }

        protected string GetBackLinkUrl() =>
            _formNavigationService.GetBackLinkUrl(CurrentPageId, TaskId, ReferenceNumber);

        /// <summary>
        /// Exposes the back link URL to Razor views that cannot call protected methods.
        /// </summary>
        public string BackLinkUrl => GetBackLinkUrl();

        protected async Task CommonFormEngineInitializationAsync()
        {
            try
            {
                await CommonInitializationAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "CommonFormEngineInitializationAsync - Error in CommonInitializationAsync");
                throw;
            }

            try
            {
                CurrentFormState = GetCurrentFormState();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "CommonFormEngineInitializationAsync - Error getting current form state");
                throw;
            }
        }
    }
}
