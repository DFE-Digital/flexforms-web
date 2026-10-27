using GovUK.Dfe.CoreLibs.Caching.Helpers;
using GovUK.Dfe.CoreLibs.Caching.Interfaces;
using GovUK.Dfe.FlexForms.Application.Interfaces;

namespace GovUK.Dfe.FlexForms.Web.Services;

/// <summary>
/// Drops this instance's cached copy of a template after a new version is saved, then reloads it.
/// </summary>
public interface ITemplateCacheInvalidator
{
    Task InvalidateAsync(string templateId);
}

public sealed class TemplateCacheInvalidator(
    IFormTemplateProvider formTemplateProvider,
    ICacheService<IMemoryCacheType> cacheService,
    ILogger<TemplateCacheInvalidator> logger) : ITemplateCacheInvalidator
{
    public async Task InvalidateAsync(string templateId)
    {
        try
        {
            var cacheKey = $"FormTemplate_{CacheKeyHelper.GenerateHashedCacheKey(templateId)}";
            logger.LogInformation("Attempting to invalidate cache for template {TemplateId} with key {CacheKey}",
                templateId, cacheKey);

            cacheService.Remove(cacheKey);
            logger.LogInformation("Successfully invalidated cache for template {TemplateId} with key {CacheKey}",
                templateId, cacheKey);

            try
            {
                await formTemplateProvider.GetTemplateAsync(templateId);
                logger.LogDebug("Successfully verified new template version is available for {TemplateId}", templateId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to verify new template version for {TemplateId}", templateId);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to invalidate cache for template {TemplateId}", templateId);
        }
    }
}
