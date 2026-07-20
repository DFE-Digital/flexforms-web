using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services
{
    public class UploadComplexFieldRenderer : IComplexFieldRenderer
    {
        public string FieldType => "upload";

        public string Render(ComplexFieldConfiguration configuration, string fieldId, string currentValue, string errorMessage, string label, string tooltip, bool isRequired)
        {
            var isRequiredAttr = isRequired ? "required" : "";
            var errorClass = !string.IsNullOrEmpty(errorMessage) ? "govuk-form-group--error" : "";
            var labelClasses = "govuk-label";

            // Generate unique IDs
            var inputId = $"{fieldId}-upload-input";
            var nameId = $"{fieldId}-name";
            var descId = $"{fieldId}-desc";
            var uploadBtnId = $"{fieldId}-upload-btn";

            return $@"
<div class=""govuk-form-group {errorClass}"">
    <fieldset class=""govuk-fieldset"">
        <legend class=""govuk-fieldset__legend {labelClasses}"">
            {label}
            {(isRequired ? "<span class=\"govuk-visually-hidden\">required</span>" : "")}
        </legend>
        {(string.IsNullOrEmpty(tooltip) ? "" : $@"<div class=""govuk-hint"">{tooltip}</div>")}
        
        {(string.IsNullOrEmpty(errorMessage) ? "" : $@"<div class=""govuk-error-message""><span class=""govuk-visually-hidden"">Error: </span>{errorMessage}</div>")}
        
    <input class=""govuk-input"" id=""{nameId}"" name=""{nameId}"" type=""text"" placeholder=""File name (optional)"" />
    <input class=""govuk-input"" id=""{descId}"" name=""{descId}"" type=""text"" placeholder=""Description (optional)"" />
    <input class=""govuk-file-upload"" id=""{inputId}"" name=""{inputId}"" type=""file"" {isRequiredAttr} />
    <button class=""govuk-button"" id=""{uploadBtnId}"" type=""submit"">Upload</button>
    <!-- Placeholder for file list and actions -->
    <div id=""{fieldId}-file-list""></div>
</div>";
        }
    }
} 