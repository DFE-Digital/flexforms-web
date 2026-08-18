using GovUK.Dfe.FlexForms.Application.Validation;
using GovUK.Dfe.FlexForms.Web.Extensions;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Extensions;

public class FormValidationResultExtensionsTests
{
    [Fact]
    public void ApplyTo_maps_field_errors_and_returns_invalid()
    {
        var modelState = new ModelStateDictionary();
        var result = new FormValidationResult([new FormValidationError("Data[name]", "Enter a name")]);

        var isValid = result.ApplyTo(modelState);

        Assert.False(isValid);
        Assert.Equal("Enter a name", modelState["Data[name]"]!.Errors.Single().ErrorMessage);
    }

    [Fact]
    public void ApplyTo_returns_true_when_there_are_no_errors()
    {
        var modelState = new ModelStateDictionary();
        Assert.True(FormValidationResult.Success.ApplyTo(modelState));
        Assert.Empty(modelState);
    }
}
