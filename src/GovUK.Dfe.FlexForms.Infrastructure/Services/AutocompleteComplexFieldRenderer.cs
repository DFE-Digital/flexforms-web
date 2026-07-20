using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services
{
    public class AutocompleteComplexFieldRenderer : IComplexFieldRenderer
    {
        public string FieldType => "autocomplete";

        public string Render(ComplexFieldConfiguration configuration, string fieldId, string currentValue, string errorMessage, string label, string tooltip, bool isRequired)
        {
            var isRequiredAttr = isRequired ? "required" : "";
            var errorClass = !string.IsNullOrEmpty(errorMessage) ? "govuk-form-group--error" : "";
            var labelClasses = "govuk-label";
            
            // Get the complex field ID from the configuration
            var complexFieldId = configuration.Id;
            
            // If complexFieldId is empty, use the fieldId as fallback
            if (string.IsNullOrEmpty(complexFieldId))
            {
                complexFieldId = fieldId;
            }
            
            // Generate unique IDs using the complex field ID
            var inputId = $"{complexFieldId}-complex-field";
            var selectId = $"{complexFieldId}-select";
            var selectedItemsId = $"{complexFieldId}-selected-items";

            return $@"
<div class=""govuk-form-group {errorClass}"">
    <label class=""{labelClasses}"" for=""{selectId}"">
        {label}
        {(isRequired ? "<span class=\"govuk-visually-hidden\">required</span>" : "")}
    </label>
    
    {(string.IsNullOrEmpty(tooltip) ? "" : $@"<div class=""govuk-hint"">{tooltip}</div>")}
    
    {(string.IsNullOrEmpty(errorMessage) ? "" : $@"<div class=""govuk-error-message""><span class=""govuk-visually-hidden"">Error: </span>{errorMessage}</div>")}
    
    <div class=""complex-field-container"" data-module=""complex-field"" data-field-type=""{configuration.FieldType}"">
        <!-- Hidden input for form submission -->
        <input type=""hidden"" 
               id=""{fieldId}"" 
               name=""Data[{fieldId}]"" 
               value=""{currentValue}"" 
               data-allow-multiple=""{configuration.AllowMultiple.ToString().ToLower()}"" />
        
        <!-- Container for autocomplete - library will create input here -->
        <div id=""{inputId}-container"" 
             class=""complex-field-search-container""
             data-complex-field-id=""{complexFieldId}""
             data-min-length=""{configuration.MinLength}""
             data-allow-multiple=""{configuration.AllowMultiple.ToString().ToLower()}""
             data-max-selections=""{configuration.MaxSelections}""
             data-target-input=""{fieldId}""
             data-selected-items-container=""{selectedItemsId}""
             data-placeholder=""{configuration.Placeholder}""
             aria-describedby=""{inputId}-hint"">
        </div>
        
        <div id=""{inputId}-hint"" class=""govuk-visually-hidden"">
            Use this field to search and select options. Type at least {configuration.MinLength} characters to see results.
            {(configuration.AllowMultiple ? "<span>You can select multiple options.</span>" : "")}
        </div>
        
        <!-- Maximum selections message -->
        {(configuration.AllowMultiple && configuration.MaxSelections > 0 ? $@"
        <div id=""{inputId}-max-message"" class=""govuk-inset-text"" style=""display: none;"">
            <p class=""govuk-body"">You have reached the maximum number of selections ({configuration.MaxSelections}). Remove a selection to add a different one.</p>
        </div>" : "")}
        
        <!-- Selected items container -->
        <div id=""{selectedItemsId}"" class=""complex-field-selected-items""></div>
    </div>
</div>";
        }
    }
} 