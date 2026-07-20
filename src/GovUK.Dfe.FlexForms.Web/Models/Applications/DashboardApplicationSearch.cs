using System.Globalization;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;

namespace GovUK.Dfe.FlexForms.Web.Models.Applications;

/// <summary>
/// Search and filter values for the applications dashboard listing.
/// </summary>
public sealed class DashboardApplicationSearch
{
    private const string DateQueryFormat = "yyyy-MM-dd";

    public string? SearchReference { get; init; }

    public string? DateStartedFromValue { get; init; }

    public string? DateStartedToValue { get; init; }

    public string? DateSubmittedFromValue { get; init; }

    public string? DateSubmittedToValue { get; init; }

    public ApplicationStatus? Status { get; init; }

    public DateTime? DateStartedFrom => ParseDate(DateStartedFromValue);

    public DateTime? DateStartedTo => ParseDate(DateStartedToValue);

    public DateTime? DateSubmittedFrom => ParseDate(DateSubmittedFromValue);

    public DateTime? DateSubmittedTo => ParseDate(DateSubmittedToValue);

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchReference) ||
        !string.IsNullOrWhiteSpace(DateStartedFromValue) ||
        !string.IsNullOrWhiteSpace(DateStartedToValue) ||
        !string.IsNullOrWhiteSpace(DateSubmittedFromValue) ||
        !string.IsNullOrWhiteSpace(DateSubmittedToValue) ||
        Status.HasValue;

    /// <summary>
    /// Builds a query string for pagination links, preserving active filters.
    /// </summary>
    public string BuildPaginationHref(int page)
    {
        var query = new List<string> { $"currentPage={page}" };

        if (!string.IsNullOrWhiteSpace(SearchReference))
            query.Add($"searchReference={Uri.EscapeDataString(SearchReference)}");

        if (!string.IsNullOrWhiteSpace(DateStartedFromValue))
            query.Add($"dateStartedFrom={Uri.EscapeDataString(DateStartedFromValue)}");

        if (!string.IsNullOrWhiteSpace(DateStartedToValue))
            query.Add($"dateStartedTo={Uri.EscapeDataString(DateStartedToValue)}");

        if (!string.IsNullOrWhiteSpace(DateSubmittedFromValue))
            query.Add($"dateSubmittedFrom={Uri.EscapeDataString(DateSubmittedFromValue)}");

        if (!string.IsNullOrWhiteSpace(DateSubmittedToValue))
            query.Add($"dateSubmittedTo={Uri.EscapeDataString(DateSubmittedToValue)}");

        if (Status.HasValue)
            query.Add($"status={(int)Status.Value}");

        return "?" + string.Join("&", query);
    }

    /// <summary>
    /// Parses HTML date input values (yyyy-MM-dd) for API calls.
    /// </summary>
    internal static DateTime? ParseDate(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && DateTime.TryParseExact(
            value.Trim(),
            DateQueryFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed.Date
            : null;
}
