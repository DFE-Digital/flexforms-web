namespace GovUK.Dfe.FlexForms.Application.Validation;

/// <summary>
/// A single field validation failure. Presentation maps this to ModelState.
/// </summary>
public sealed record FormValidationError(string FieldKey, string Message);
