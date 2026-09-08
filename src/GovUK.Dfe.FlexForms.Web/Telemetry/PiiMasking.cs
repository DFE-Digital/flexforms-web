using System.Text.RegularExpressions;

namespace GovUK.Dfe.FlexForms.Web.Telemetry;

/// <summary>
/// Masks personally identifiable information before it is written to logs or Application Insights.
/// </summary>
public static partial class PiiMasking
{
    private static readonly string[] EmailPropertyKeys =
    [
        "UserEmail",
        "Email",
        "ToEmail",
        "SubmittedByEmail",
        "SubmittedByUserEmail",
        "user_email"
    ];

    public static bool IsEmailPropertyKey(string key) =>
        EmailPropertyKeys.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));

    public static bool LooksLikeEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return EmailPattern().IsMatch(value.Trim());
    }

    /// <summary>
    /// Masks an email address, keeping the first two and last five characters visible.
    /// </summary>
    public static string MaskEmail(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        var email = value.Trim();
        if (!LooksLikeEmail(email))
            return value;

        if (email.Length <= 7)
            return email.Length <= 2
                ? email
                : email[..2] + new string('*', email.Length - 2);

        var prefix = email[..2];
        var suffix = email[^5..];
        var maskedMiddle = new string('*', email.Length - 7);
        return prefix + maskedMiddle + suffix;
    }

    public static string MaskIfEmailProperty(string key, string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        return IsEmailPropertyKey(key) || LooksLikeEmail(value)
            ? MaskEmail(value)
            : value;
    }

    public static string MaskEmailsInText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? string.Empty;

        return EmailPattern().Replace(text, match => MaskEmail(match.Value));
    }

    [GeneratedRegex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled)]
    private static partial Regex EmailPattern();
}
