using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services
{
    public class ComplexFieldRendererFactory(IEnumerable<IComplexFieldRenderer> renderers)
        : IComplexFieldRendererFactory
    {
        public IComplexFieldRenderer GetRenderer(string fieldType)
        {
            var renderer = renderers.FirstOrDefault(r => r.FieldType.Equals(fieldType, StringComparison.OrdinalIgnoreCase));
            // Unknown / missing type must not accidentally pick upload just because it is registered;
            // default to autocomplete (same as ComplexFieldConfiguration.FieldType default).
            return renderer
                   ?? renderers.FirstOrDefault(r => r.FieldType.Equals("autocomplete", StringComparison.OrdinalIgnoreCase));
        }
    }
} 