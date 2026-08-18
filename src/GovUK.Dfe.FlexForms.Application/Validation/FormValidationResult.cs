namespace GovUK.Dfe.FlexForms.Application.Validation;

/// <summary>
/// Outcome of form-engine validation without ASP.NET ModelState.
/// </summary>
public sealed class FormValidationResult
{
    public static FormValidationResult Success { get; } = new([]);

    public FormValidationResult(IReadOnlyList<FormValidationError> errors)
    {
        Errors = errors;
    }

    public IReadOnlyList<FormValidationError> Errors { get; }

    public bool IsValid => Errors.Count == 0;
}
