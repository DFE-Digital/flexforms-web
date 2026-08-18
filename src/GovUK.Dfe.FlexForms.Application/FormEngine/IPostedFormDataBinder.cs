namespace GovUK.Dfe.FlexForms.Application.FormEngine;

/// <summary>
/// Maps posted <c>Data[field]</c> keys (including GOV.UK date parts) into the form data dictionary.
/// Date composition is a separate step so conditional logic can run first, matching the PageModel order.
/// </summary>
public interface IPostedFormDataBinder
{
    Dictionary<string, object> Bind(
        IReadOnlyDictionary<string, IReadOnlyList<string>> formFields,
        Dictionary<string, object>? existing = null);

    void ApplyDateParts(
        IReadOnlyDictionary<string, IReadOnlyList<string>> formFields,
        Dictionary<string, object> data);
}
