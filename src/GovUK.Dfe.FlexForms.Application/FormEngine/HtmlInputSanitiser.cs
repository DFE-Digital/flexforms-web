using System.Text.Encodings.Web;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

/// <summary>
/// Encodes posted text to prevent XSS and normalises newlines to <c>&lt;br&gt;</c>.
/// </summary>
public static class HtmlInputSanitiser
{
    public static string Sanitise(string input)
    {
        var lines = input.Split("\r\n").SelectMany(s => s.Split('\r')).SelectMany(s => s.Split('\n'));
        return string.Join("<br>", lines.Select(HtmlEncoder.Default.Encode));
    }
}
