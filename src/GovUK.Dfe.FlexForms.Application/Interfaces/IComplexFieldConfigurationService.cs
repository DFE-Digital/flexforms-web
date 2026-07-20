using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Application.Interfaces
{
    public interface IComplexFieldConfigurationService
    {
        ComplexFieldConfiguration GetConfiguration(string complexFieldId);
        bool HasConfiguration(string complexFieldId);
    }
} 