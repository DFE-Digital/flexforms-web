using System.Text.Json;

namespace GovUK.Dfe.FlexForms.Web.Pages.FormEngine;

public static class JsonHelpers
{
    /// <summary>
    /// <para>
    /// Evaluates a JSON path on a given JSON element and retrieves the corresponding nested JSON element.
    /// </para>
    /// <para>
    /// Note: This method is not a compliant implementation of the JSONPath specification.
    /// Only dot-separated paths are supported, e.g. <c>"foo.bar.baz"</c>.
    /// </para>
    /// </summary>
    /// <param name="element">
    /// The JSON element on which the path evaluation is performed. Must have a <c>ValueKind</c> of <c>Object</c>.
    /// </param>
    /// <param name="path">
    /// A string specifying the path to a nested JSON property (e.g., <c>"property.subproperty"</c>).
    /// </param>
    /// <returns>
    /// The JSON element corresponding to the specified path if it exists and is valid; otherwise, null.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown if the method is invoked on a JSON element whose <c>ValueKind</c> is not <c>Object</c>.
    /// </exception>
    public static JsonElement? EvaluatePath(this JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException(
                $"This method should only be used on JSON objects, but the element is a {element.ValueKind.DisplayName()}",
                nameof(element)
            );
        }
        
        while (true)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            
            var pathSegments = path.Split('.', 2);
            if (!element.TryGetProperty(pathSegments[0], out var result))
            {
                return null;
            }

            if (pathSegments.Length == 1) return result;
            element = result;
            path = pathSegments[1];
        }
    }

    private static string DisplayName(this JsonValueKind kind) => kind switch
    {
        JsonValueKind.Undefined => "undefined",
        JsonValueKind.Object => "object",
        JsonValueKind.Array => "array",
        JsonValueKind.String => "string",
        JsonValueKind.Number => "number",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        JsonValueKind.Null => "null",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}