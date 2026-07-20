namespace GovUK.Dfe.FlexForms.Application.Options;

public class DashboardOptions
{
    public int PageSize { get; set; } = 50;

    /// <summary>
    /// When enabled, shows the application listing filter panel on the dashboard.
    /// </summary>
    public bool EnableApplicationFilters { get; set; }
}
