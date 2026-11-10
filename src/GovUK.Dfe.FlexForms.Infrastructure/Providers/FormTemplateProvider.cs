using GovUK.Dfe.CoreLibs.Caching.Helpers;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Caching;
using GovUK.Dfe.FlexForms.Domain.Models;
using System.Diagnostics.CodeAnalysis;

namespace GovUK.Dfe.FlexForms.Infrastructure.Providers;

/// <summary>
/// Latest template version, cached briefly in each replica's memory and for longer in Redis. A save removes both on
/// every replica (<c>ITemplateCacheInvalidator</c>). If Redis is unavailable the template is loaded from the API.
/// </summary>
public class FormTemplateProvider(
    ITemplateStore store, 
    IFormTemplateParser parser,
    ITwoLevelCache cacheService) : IFormTemplateProvider
{
    [ExcludeFromCodeCoverage]
    public async Task<FormTemplate> GetTemplateAsync(string templateId, CancellationToken cancellationToken = default)
    {
        var cacheKey = FlexFormsCacheKeys.FormTemplate(CacheKeyHelper.GenerateHashedCacheKey(templateId));
        var methodName = nameof(GetTemplateAsync);

        return await cacheService.GetOrAddAsync(
            cacheKey,
            async () =>
            {
                using var stream = await store.GetTemplateStreamAsync(templateId, cancellationToken);
                return await parser.ParseAsync(stream, cancellationToken);
            },
            methodName,
            cancellationToken);
    }
}
