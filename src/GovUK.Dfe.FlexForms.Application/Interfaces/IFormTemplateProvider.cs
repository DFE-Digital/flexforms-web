using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Application.Interfaces;

public interface IFormTemplateProvider
{
    Task<FormTemplate> GetTemplateAsync(string templateId, CancellationToken cancellationToken = default);
}