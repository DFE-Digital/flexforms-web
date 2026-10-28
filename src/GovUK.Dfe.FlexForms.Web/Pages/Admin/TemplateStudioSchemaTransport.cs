using System.Text;

namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Template Studio sends the template JSON as base64 with a "b64:" prefix. Raw JSON (quotes, dashes, words like
/// "select") trips the Azure WAF's SQL injection and XSS rules, which block the request before it reaches the app.
/// Values without the prefix are plain JSON, so older copies of the script still work.
/// </summary>
public static class TemplateStudioSchemaTransport
{
    public const string Prefix = "b64:";

    public static string? Decode(string? value)
    {
        if (value is null || !value.StartsWith(Prefix, StringComparison.Ordinal))
            return value;

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value[Prefix.Length..]));
        }
        catch (FormatException)
        {
            return value;
        }
    }
}
