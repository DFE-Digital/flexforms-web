namespace GovUK.Dfe.FlexForms.Application.Options;

/// <summary>
/// Configuration for the applications dashboard listing and display copy.
/// Bound from the <c>Dashboard</c> section in tenant appsettings.
/// </summary>
public class DashboardOptions
{
    public int PageSize { get; set; } = 50;

    /// <summary>
    /// When enabled, shows the application listing filter panel on the dashboard.
    /// </summary>
    public bool EnableApplicationFilters { get; set; }

    /// <summary>
    /// Main page heading (e.g. "Your visits"). When empty, falls back to terminology-based default.
    /// </summary>
    public string? MainHeading { get; set; }

    /// <summary>
    /// Heading above the in-progress list (e.g. "Visits in progress"). When empty, falls back to terminology-based default.
    /// </summary>
    public string? InProgressHeading { get; set; }

    /// <summary>
    /// Heading for the start-new section (e.g. "Start a new visit"). When empty, falls back to terminology-based default.
    /// </summary>
    public string? StartNewHeading { get; set; }

    /// <summary>
    /// Supporting text under the start-new heading. When empty, falls back to terminology-based default.
    /// </summary>
    public string? StartNewHint { get; set; }

    /// <summary>
    /// Label for the start-new primary button. When empty, falls back to terminology-based default.
    /// </summary>
    public string? StartNewButtonText { get; set; }
}
