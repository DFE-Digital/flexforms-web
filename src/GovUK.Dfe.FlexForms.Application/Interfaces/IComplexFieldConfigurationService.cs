using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Application.Interfaces
{
    public interface IComplexFieldConfigurationService
    {
        ComplexFieldConfiguration GetConfiguration(string complexFieldId);
        bool HasConfiguration(string complexFieldId);

        /// <summary>
        /// Every complex field configured for the current tenant, one per id.
        /// </summary>
        IReadOnlyList<ComplexFieldConfiguration> GetAllConfigurations();
    }
} 