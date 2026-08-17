using Microsoft.AspNetCore.Http;

namespace GovUK.Dfe.FlexForms.Web.Extensions;

public static class FormCollectionExtensions
{
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> ToPostedFields(this IFormCollection form)
    {
        var fields = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var key in form.Keys)
            fields[key] = form[key].ToArray();

        return fields;
    }
}
