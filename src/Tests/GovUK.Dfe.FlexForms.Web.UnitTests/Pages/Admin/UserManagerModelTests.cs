using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Web.Pages.Admin;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages.Admin;

public class UserManagerModelTests
{
    private readonly IUserManagerAdmin _admin = Substitute.For<IUserManagerAdmin>();
    private readonly UserManagerModel _model;

    public UserManagerModelTests()
    {
        _model = new UserManagerModel(_admin);

        var httpContext = Substitute.For<HttpContext>();
        _model.PageContext = new PageContext
        {
            HttpContext = httpContext,
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        };
        _model.TempData = new TempDataDictionary(httpContext, Substitute.For<ITempDataProvider>());
    }

    [Fact]
    public async Task OnGetAsync_ShouldPassFiltersToTheAdminService()
    {
        _model.CurrentPage = 2;
        _model.SearchTerm = "brown";
        _model.Role = "Admin";

        await _model.OnGetAsync(CancellationToken.None);

        await _admin.Received(1).LoadAsync(
            Arg.Is<UserManagerWorkState>(s =>
                s.CurrentPage == 2 && s.SearchTerm == "brown" && s.Role == "Admin"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnGetAsync_ShouldSurfaceAvailableRolesAndResultCounts()
    {
        _admin.LoadAsync(Arg.Any<UserManagerWorkState>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var state = callInfo.Arg<UserManagerWorkState>();
                state.Users = [new TenantUserDto { UserId = Guid.NewGuid(), Name = "Ada", Email = "ada@example.test" }];
                state.AvailableRoles = ["Admin", "User"];
                state.TotalCount = 1;
                state.TotalPages = 1;
                state.CurrentPage = 1;
                return Task.CompletedTask;
            });

        await _model.OnGetAsync(CancellationToken.None);

        Assert.Equal(["Admin", "User"], _model.AvailableRoles);
        Assert.Equal(1, _model.TotalCount);
        Assert.Single(_model.Users);
    }

    [Fact]
    public async Task OnGetAsync_ShouldRejectOverlongSearchTerm_AndNotSendItToTheApi()
    {
        _model.SearchTerm = new string('a', 300);

        await _model.OnGetAsync(CancellationToken.None);

        Assert.False(_model.ModelState.IsValid);
        Assert.True(_model.ShowFiltersPanel);
        await _admin.Received(1).LoadAsync(
            Arg.Is<UserManagerWorkState>(s => s.SearchTerm == null),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null, null, "?currentPage=3")]
    [InlineData("brown", null, "?currentPage=3&searchTerm=brown")]
    [InlineData("a b", "Admin", "?currentPage=3&searchTerm=a%20b&role=Admin")]
    public void BuildPaginationHref_ShouldPreserveActiveFilters(
        string? searchTerm,
        string? role,
        string expected)
    {
        _model.SearchTerm = searchTerm;
        _model.Role = role;

        Assert.Equal(expected, _model.BuildPaginationHref(3));
    }

    [Fact]
    public void IsSearchActive_ShouldBeFalse_WhenNoFiltersAreSet()
    {
        Assert.False(_model.IsSearchActive);
        Assert.False(_model.ShowFiltersPanel);
    }

    [Fact]
    public async Task OnPostRemoveAsync_ShouldRedirectPreservingFilters()
    {
        _model.CurrentPage = 2;
        _model.SearchTerm = "brown";
        _model.Role = "Admin";
        _admin.RemoveAsync(Arg.Any<UserManagerWorkState>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(AdminPageOutcome.Redirect(successMessage: "done"));

        var result = await _model.OnPostRemoveAsync(Guid.NewGuid(), CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(2, redirect.RouteValues!["currentPage"]);
        Assert.Equal("brown", redirect.RouteValues["searchTerm"]);
        Assert.Equal("Admin", redirect.RouteValues["role"]);
    }
}
