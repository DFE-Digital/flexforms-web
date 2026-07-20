using GovUK.Dfe.FlexForms.Application.Interfaces;
using System.Diagnostics.CodeAnalysis;

namespace GovUK.Dfe.FlexForms.Infrastructure.Stores;

public class JsonFileTemplateStore(string basePath) : ITemplateStore
{
    [ExcludeFromCodeCoverage]
    public Task<Stream> GetTemplateStreamAsync(string templateId, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(basePath, $"{templateId}.json");
        Stream stream = File.OpenRead(path);
        return Task.FromResult(stream);
    }
}