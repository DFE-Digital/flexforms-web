namespace GovUK.Dfe.FlexForms.Application.Options;

/// <summary>
/// Configuration for displaying a site-wide GOV.UK notification banner across all pages.
/// When <see cref="Enabled"/> is <c>true</c>, a notification banner is rendered at the top
/// of every page with the configured <see cref="Message"/> text.
/// Bound from the <c>NotificationBanner</c> section in appsettings.
/// </summary>
public class NotificationBannerOptions
{
    /// <summary>
    /// Whether the notification banner should be displayed on all pages.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The heading text displayed inside the notification banner (e.g. "Important").
    /// </summary>
    public string Heading { get; set; } = "Important";

    /// <summary>
    /// The message text displayed inside the notification banner content area.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
