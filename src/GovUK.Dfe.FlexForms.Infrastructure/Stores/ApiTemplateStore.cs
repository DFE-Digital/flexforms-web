using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace GovUK.Dfe.FlexForms.Infrastructure.Stores;

public class ApiTemplateStore(ITemplatesClient templateClient) : ITemplateStore
{
    [ExcludeFromCodeCoverage]
    public async Task<Stream> GetTemplateStreamAsync(string templateId, CancellationToken cancellationToken = default)
    {
        var response = await templateClient.GetLatestTemplateSchemaAsync(new Guid(templateId), cancellationToken);
        var utf8 = Encoding.UTF8.GetBytes(response.JsonSchema);
        return new MemoryStream(utf8);
    }
}