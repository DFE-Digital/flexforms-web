using GovUK.Dfe.CoreLibs.Caching.Helpers;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Caching;

namespace GovUK.Dfe.FlexForms.Web.Services;

/// <summary>
/// Drops every cached copy of a template (Redis, and memory on every replica) after a new version is saved, then reloads it.
/// </summary>
public interface ITemplateCacheInvalidator
{
    Task InvalidateAsync(string templateId);
}

public sealed class TemplateCacheInvalidator(
    IFormTemplateProvider formTemplateProvider,
    ITwoLevelCache cache,
    ILogger<TemplateCacheInvalidator> logger) : ITemplateCacheInvalidator
{
    public async Task InvalidateAsync(string templateId)
    {
        try
        {
            var cacheKey = FlexFormsCacheKeys.FormTemplate(CacheKeyHelper.GenerateHashedCacheKey(templateId));
            logger.LogInformation("Attempting to invalidate cache for template {TemplateId} with key {CacheKey}",
                templateId, cacheKey);

            await cache.RemoveAsync(cacheKey);
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
