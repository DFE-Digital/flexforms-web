using GovUK.Dfe.FlexForms.Domain.FormEngine;
using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Domain.Tests.FormEngine;

public class PageNavigationPolicyTests
{
    [Fact]
    public void Resolve_ShouldUseNavigationAfterSave_WhenSet()
    {
        var page = Page(returnToSummaryPage: true, NavigationAfterSave.Linear);
        Assert.Equal(NavigationAfterSave.Linear, PageNavigationPolicy.Resolve(page));

        page.NavigationAfterSave = NavigationAfterSave.Branch;
        page.ReturnToSummaryPage = false;
        Assert.Equal(NavigationAfterSave.Branch, PageNavigationPolicy.Resolve(page));

        page.NavigationAfterSave = NavigationAfterSave.Summary;
        Assert.Equal(NavigationAfterSave.Summary, PageNavigationPolicy.Resolve(page));
    }

    [Fact]
    public void Resolve_ShouldFallBackToReturnToSummaryPage_WhenNavigationAfterSaveOmitted()
    {
        Assert.Equal(
            NavigationAfterSave.Summary,
            PageNavigationPolicy.Resolve(Page(returnToSummaryPage: true)));
        Assert.Equal(
            NavigationAfterSave.Linear,
            PageNavigationPolicy.Resolve(Page(returnToSummaryPage: false)));
    }

    private static Page Page(bool returnToSummaryPage, NavigationAfterSave? navigationAfterSave = null) =>
        new()
        {
            PageId = "p1",
            Slug = "p1",
            Title = "p1",
            Description = "p1",
            PageOrder = 1,
            Fields = [],
            ReturnToSummaryPage = returnToSummaryPage,
            NavigationAfterSave = navigationAfterSave
        };
}
