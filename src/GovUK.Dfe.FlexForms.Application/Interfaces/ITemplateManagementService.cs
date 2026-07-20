using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;

namespace GovUK.Dfe.FlexForms.Application.Interfaces
{
    /// <summary>
    /// Service for managing form templates and schema operations
    /// </summary>
    public interface ITemplateManagementService
    {
        /// <summary>
        /// Loads the appropriate template based on whether this is a new or existing application
        /// </summary>
        Task<FormTemplate> LoadTemplateAsync(string templateId, ApplicationDto? currentApplication = null);

        /// <summary>
        /// Converts a template schema JSON to a FormTemplate
        /// </summary>
        Task<FormTemplate> ParseTemplateFromSchemaAsync(string templateSchema);

        /// <summary>
        /// Finds a specific task in the template
        /// </summary>
        (TaskGroup Group, Domain.Models.Task Task) FindTask(FormTemplate template, string taskId);

        /// <summary>
        /// Finds a specific page in the template
        /// </summary>
        (TaskGroup Group, Domain.Models.Task Task, Domain.Models.Page Page) FindPage(FormTemplate template, string pageId);

        
    }
} 