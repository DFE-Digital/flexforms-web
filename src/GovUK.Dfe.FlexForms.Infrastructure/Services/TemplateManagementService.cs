using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services
{
    /// <summary>
    /// Implementation of template management service for handling form templates and schema operations
    /// </summary>
    public class TemplateManagementService(
        IFormTemplateProvider templateProvider,
        IFormTemplateParser templateParser,
        ILogger<TemplateManagementService> logger)
        : ITemplateManagementService
    {
        public async Task<FormTemplate> LoadTemplateAsync(string templateId, ApplicationDto? currentApplication = null)
        {
            try
            {
                // If we have an existing application with template schema, use that version
                if (currentApplication?.TemplateSchema != null)
                {
                    logger.LogDebug("Using template schema from existing application {ApplicationId} with template version {TemplateVersionId}", 
                        currentApplication.ApplicationId, currentApplication.TemplateVersionId);
                    
                    return await ParseTemplateFromSchemaAsync(currentApplication.TemplateSchema.JsonSchema);
                }
                else
                {
                    // For new applications or when template schema is not available, use the latest template
                    logger.LogDebug("Loading latest template schema for template {TemplateId}", templateId);
                    return await templateProvider.GetTemplateAsync(templateId);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load template {TemplateId}", templateId);
                throw;
            }
        }

        public async Task<FormTemplate> ParseTemplateFromSchemaAsync(string templateSchema)
        {
            try
            {
                // Convert to stream for parser
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(templateSchema));
                
                // Use the same parser that's used for API templates to ensure consistency
                return await templateParser.ParseAsync(stream);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to parse template schema from application");
                throw new InvalidOperationException("Failed to parse template schema from application", ex);
            }
        }

        public (TaskGroup Group, Domain.Models.Task Task) FindTask(FormTemplate template, string taskId)
        {
            var allTasks = template.TaskGroups
                .SelectMany(g => g.Tasks.Select(t => new { Group = g, Task = t }))
                .ToList();

            var taskPair = allTasks.FirstOrDefault(x => x.Task.TaskId == taskId);
            
            if (taskPair == null)
            {
                throw new InvalidOperationException($"Task with ID '{taskId}' not found.");
            }

            return (taskPair.Group, taskPair.Task);
        }

        public (TaskGroup Group, Domain.Models.Task Task, Domain.Models.Page Page) FindPage(FormTemplate template, string pageId)
        {
            var allPages = template.TaskGroups
                .SelectMany(g => g.Tasks)
                .SelectMany(t => t.Pages ?? new List<Domain.Models.Page>())
                .ToList();

            var currentPage = allPages.FirstOrDefault(p => p.PageId == pageId) ?? allPages.First();

            var pair = template.TaskGroups
                .SelectMany(g => g.Tasks.Select(t => new { Group = g, Task = t }))
                .First(x => x.Task.Pages?.Contains(currentPage) == true);

            return (pair.Group, pair.Task, currentPage);
        }

        
    }
} 