using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Application.Interfaces;

public interface IFormTemplateParser
{
    Task<FormTemplate> ParseAsync(Stream templateStream, CancellationToken cancellationToken = default);
}