using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Application.Interfaces
{
    public interface IComplexFieldRenderer
    {
        string FieldType { get; }
        string Render(ComplexFieldConfiguration configuration, string complexFieldId, string currentValue, string errorMessage, string label, string tooltip, bool isRequired);
    }
} 