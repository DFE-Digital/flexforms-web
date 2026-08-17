namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// HTTP-agnostic dropdown option. The PageModel maps this to <c>SelectListItem</c>.
/// </summary>
public sealed record AdminSelectOption(string Text, string Value, bool Selected);
