namespace GovUK.Dfe.FlexForms.Application.Interfaces;

public interface ITemplateStore
{
    Task<Stream> GetTemplateStreamAsync(string templateId, CancellationToken cancellationToken = default);
}
