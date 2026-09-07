using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.FlexForms.Web.Models.Applications;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Models;

public class DashboardApplicationSearchTests
{
    [Fact]
    public void ParseDate_ShouldAcceptIsoDatesAndRejectInvalidValues()
    {
        Assert.Equal(new DateTime(2026, 4, 1), DashboardApplicationSearch.ParseDate("2026-04-01"));
        Assert.Null(DashboardApplicationSearch.ParseDate("01/04/2026"));
        Assert.Null(DashboardApplicationSearch.ParseDate(" "));
        Assert.Null(DashboardApplicationSearch.ParseDate(null));
    }

    [Fact]
    public void BuildPaginationHref_ShouldPreserveActiveFilters()
    {
        var search = new DashboardApplicationSearch
        {
            SearchReference = "REF 1",
            DateStartedFromValue = "2026-01-01",
            DateStartedToValue = "2026-02-01",
            DateSubmittedFromValue = "2026-03-01",
            DateSubmittedToValue = "2026-04-01",
            Status = ApplicationStatus.InProgress
        };

        var href = search.BuildPaginationHref(2);

        Assert.True(search.HasActiveFilters);
        Assert.Equal(new DateTime(2026, 1, 1), search.DateStartedFrom);
        Assert.Contains("currentPage=2", href);
        Assert.Contains("searchReference=REF%201", href);
        Assert.Contains("dateStartedFrom=2026-01-01", href);
        Assert.Contains("status=", href);
    }

    [Fact]
    public void HasActiveFilters_ShouldBeFalse_WhenEmpty()
    {
        var search = new DashboardApplicationSearch();

        Assert.False(search.HasActiveFilters);
        Assert.Equal("?currentPage=1", search.BuildPaginationHref(1));
    }
}
