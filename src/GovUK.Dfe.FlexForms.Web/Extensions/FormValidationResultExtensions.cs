using GovUK.Dfe.FlexForms.Application.Validation;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace GovUK.Dfe.FlexForms.Web.Extensions;

/// <summary>
/// Maps Application validation results onto ASP.NET ModelState.
/// </summary>
public static class FormValidationResultExtensions
{
    public static bool ApplyTo(this FormValidationResult result, ModelStateDictionary modelState)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(modelState);

        foreach (var error in result.Errors)
            modelState.AddModelError(error.FieldKey, error.Message);

        return result.IsValid;
    }
}
