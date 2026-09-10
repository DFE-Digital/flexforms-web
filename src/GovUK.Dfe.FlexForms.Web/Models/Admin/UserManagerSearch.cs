namespace GovUK.Dfe.FlexForms.Web.Models.Admin;

/// <summary>
/// Search and filter values for the User Manager listing.
/// </summary>
public sealed class UserManagerSearch
{
    /// <summary>
    /// Longest search term the API will accept.
    /// </summary>
    public const int MaxSearchTermLength = 256;

    public string? SearchTerm { get; init; }

    public string? Role { get; init; }

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchTerm) || !string.IsNullOrWhiteSpace(Role);

    /// <summary>
    /// Builds a query string for pagination links, preserving active filters.
    /// </summary>
    public string BuildPaginationHref(int page)
    {
        var query = new List<string> { $"currentPage={page}" };

        if (!string.IsNullOrWhiteSpace(SearchTerm))
            query.Add($"searchTerm={Uri.EscapeDataString(SearchTerm)}");

        if (!string.IsNullOrWhiteSpace(Role))
            query.Add($"role={Uri.EscapeDataString(Role)}");

        return "?" + string.Join("&", query);
    }
}
