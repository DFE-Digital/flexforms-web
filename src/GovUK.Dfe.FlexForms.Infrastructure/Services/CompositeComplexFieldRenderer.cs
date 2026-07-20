using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services
{
    public class CompositeComplexFieldRenderer : IComplexFieldRenderer
    {
        public string FieldType => "composite";

        public string Render(ComplexFieldConfiguration configuration, string complexFieldId, string currentValue, string errorMessage, string label, string tooltip, bool isRequired)
        {
            var errorClass = !string.IsNullOrEmpty(errorMessage) ? "govuk-form-group--error" : "";
            var labelClasses = "govuk-label";
            
            // Get sub-fields from additional properties
            var subFields = configuration.AdditionalProperties.ContainsKey("SubFields") 
                ? configuration.AdditionalProperties["SubFields"].ToString() 
                : "";

            return $@"
<div class=""govuk-form-group {errorClass}"">
    <fieldset class=""govuk-fieldset"">
        <legend class=""govuk-fieldset__legend {labelClasses}"">
            {label}
            {(isRequired ? "<span class=\"govuk-visually-hidden\">required</span>" : "")}
        </legend>
        
        {(string.IsNullOrEmpty(tooltip) ? "" : $@"<div class=""govuk-hint"">{tooltip}</div>")}
        
        {(string.IsNullOrEmpty(errorMessage) ? "" : $@"<div class=""govuk-error-message""><span class=""govuk-visually-hidden"">Error: </span>{errorMessage}</div>")}
        
        <div class=""govuk-form-group"" data-module=""composite-field"">
            <!-- Composite field content would be rendered here based on SubFields configuration -->
            <div class=""govuk-hint"">Composite field - {subFields}</div>
        </div>
    </fieldset>
</div>";
        }
    }
} 