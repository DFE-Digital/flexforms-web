using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Application.Dashboard;
using GovUK.Dfe.FlexForms.Application.Options;
using GovUK.Dfe.FlexForms.Web.Pages.Applications;
using GovUK.Dfe.FlexForms.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages.Applications;

public class IndexModelTests
{
    private readonly IDashboardApplications _dashboardApplications = Substitute.For<IDashboardApplications>();
    private readonly IApplicationStatusService _applicationStatusService = Substitute.For<IApplicationStatusService>();
    private readonly ITemplateSelectionService _templateSelection = Substitute.For<ITemplateSelectionService>();
    private readonly IOptions<DashboardOptions> _options = Options.Create(new DashboardOptions { PageSize = 10, EnableApplicationFilters = true });
    private readonly IndexModel _model;

    public IndexModelTests()
    {
        _applicationStatusService.GetBaseApplicationStatuses().Returns(new List<KeyValuePair<ApplicationStatus, string>>
        {
            new(ApplicationStatus.InProgress, "In progress"),
            new(ApplicationStatus.Deleted, "Deleted")
        });

        _templateSelection.GetSelectableTemplatesAsync(Arg.Any<CancellationToken>()).Returns([]);

        _model = CreateModel(_options);
    }

    private IndexModel CreateModel(IOptions<DashboardOptions> options)
    {
        var model = new IndexModel(
            _dashboardApplications,
            _applicationStatusService,
            _templateSelection,
            options,
            NullLogger<IndexModel>.Instance);

        var httpContext = Substitute.For<HttpContext>();
        httpContext.Session.Returns(Substitute.For<ISession>());
        model.PageContext = new PageContext
        {
            HttpContext = httpContext,
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        };
        model.TempData = new TempDataDictionary(httpContext, Substitute.For<ITempDataProvider>());
        return model;
    }

    private void SelectTemplateInSession(Guid templateId, string name = "Transfers")
    {
        _templateSelection.GetSelectedTemplateId(Arg.Any<HttpContext>()).Returns(templateId.ToString());
        _templateSelection.GetSelectedTemplateName(Arg.Any<HttpContext>()).Returns(name);
    }

    private void AccessibleTemplates(params TemplateDto[] templates) =>
        _templateSelection.GetSelectableTemplatesAsync(Arg.Any<CancellationToken>()).Returns(templates);

    private static TemplateDto Template(Guid templateId, string name) =>
        new() { TemplateId = templateId, Name = name, CreatedOn = DateTime.UtcNow };

    private void ReturnsApplications(params ApplicationWithCalculatedStatus[] applications) =>
        _dashboardApplications.ListAsync(Arg.Any<DashboardApplicationListQuery>())
            .Returns(new DashboardApplicationListResult
            {
                Applications = applications,
                TotalPages = 1,
                CurrentPage = 1
            });

    [Fact]
    public async Task OnGetAsync_when_template_missing_sets_no_applications()
    {
        await _model.OnGetAsync();

        Assert.Empty(_model.Applications);
    }

    [Fact]
    public async Task OnGetAsync_with_template_filters_deleted_applications_when_not_admin()
    {
        SelectTemplateInSession(Guid.NewGuid());

        var app1 = new ApplicationDto { ApplicationId = Guid.NewGuid(), ApplicationReference = "R1", DateCreated = DateTime.UtcNow.AddDays(-1) };
        var app2 = new ApplicationDto { ApplicationId = Guid.NewGuid(), ApplicationReference = "R2", DateCreated = DateTime.UtcNow };

        ReturnsApplications(
            new ApplicationWithCalculatedStatus { Application = app1, CalculatedStatus = new KeyValuePair<ApplicationStatus, string>(ApplicationStatus.Deleted, "Deleted") },
            new ApplicationWithCalculatedStatus { Application = app2, CalculatedStatus = new KeyValuePair<ApplicationStatus, string>(ApplicationStatus.InProgress, "In progress") });

        // non-admin user
        _model.PageContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity("Test"));

        await _model.OnGetAsync();

        Assert.Single(_model.Applications);
        Assert.Equal("R2", _model.Applications.First().ApplicationReference);
        Assert.Equal(1, _model.TotalPages);
    }

    [Fact]
    public async Task OnGetAsync_with_invalid_date_filters_leaves_no_applications()
    {
        SelectTemplateInSession(Guid.NewGuid());
        _model.DateStartedFrom = "not-a-date";

        // ensure dashboardApplications would return something if called
        ReturnsApplications();

        await _model.OnGetAsync();

        Assert.False(_model.ModelState.IsValid);
        Assert.Contains("DateStartedFrom", _model.ModelState);
        Assert.Empty(_model.Applications);
    }

    [Fact]
    public async Task OnGetAsync_admin_sees_deleted_status_in_status_filters()
    {
        SelectTemplateInSession(Guid.NewGuid());

        // admin user
        _model.PageContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Admin") }, "Test"));

        _applicationStatusService.GetCustomApplicationStatusesAsync(Arg.Any<Guid?>())
            .Returns(new List<CustomApplicationStatusDto>());

        ReturnsApplications();

        await _model.OnGetAsync();

        Assert.Contains(_model.StatusFilters, s => s.Key == ApplicationStatus.Deleted);
    }

    [Fact]
    public async Task OnGetAsync_when_filters_disabled_loads_applications_without_validation()
    {
        var modelNoFilters = CreateModel(
            Options.Create(new DashboardOptions { PageSize = 10, EnableApplicationFilters = false }));

        SelectTemplateInSession(Guid.NewGuid());
        ReturnsApplications();

        modelNoFilters.DateStartedFrom = "not-a-date";

        await modelNoFilters.OnGetAsync();

        Assert.True(modelNoFilters.ModelState.IsValid);
        await _dashboardApplications.Received().ListAsync(Arg.Any<DashboardApplicationListQuery>());
    }

    [Fact]
    public async Task OnGetAsync_lists_applications_for_the_selected_template_instead_of_the_session_one()
    {
        var sessionTemplateId = Guid.NewGuid();
        var otherTemplateId = Guid.NewGuid();
        SelectTemplateInSession(sessionTemplateId);
        AccessibleTemplates(
            Template(sessionTemplateId, "Transfers"),
            Template(otherTemplateId, "Conversions"));
        ReturnsApplications();

        _model.SelectedTemplateId = otherTemplateId;

        await _model.OnGetAsync();

        Assert.Equal(otherTemplateId, _model.TemplateId);
        Assert.Equal("Conversions", _model.TemplateName);
        await _dashboardApplications.Received().ListAsync(
            Arg.Is<DashboardApplicationListQuery>(q => q.TemplateId == otherTemplateId));
    }

    [Fact]
    public async Task OnGetAsync_ignores_a_template_the_user_cannot_access()
    {
        var sessionTemplateId = Guid.NewGuid();
        SelectTemplateInSession(sessionTemplateId);
        AccessibleTemplates(Template(sessionTemplateId, "Transfers"));
        ReturnsApplications();

        _model.SelectedTemplateId = Guid.NewGuid();

        await _model.OnGetAsync();

        Assert.Null(_model.SelectedTemplateId);
        Assert.Equal(sessionTemplateId, _model.TemplateId);
        await _dashboardApplications.Received().ListAsync(
            Arg.Is<DashboardApplicationListQuery>(q => q.TemplateId == sessionTemplateId));
    }

    [Fact]
    public async Task OnGetAsync_falls_back_to_the_session_template_when_templates_cannot_be_loaded()
    {
        var sessionTemplateId = Guid.NewGuid();
        SelectTemplateInSession(sessionTemplateId);
        _templateSelection.GetSelectableTemplatesAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<TemplateDto>>(_ => throw new InvalidOperationException("boom"));
        ReturnsApplications();

        await _model.OnGetAsync();

        Assert.Equal(sessionTemplateId, _model.TemplateId);
        Assert.Empty(_model.TemplateOptions);
        Assert.False(_model.ShowTemplateFilter);
    }

    [Fact]
    public async Task OnGetAsync_hides_the_template_filter_when_only_one_template_is_accessible()
    {
        var sessionTemplateId = Guid.NewGuid();
        SelectTemplateInSession(sessionTemplateId);
        AccessibleTemplates(Template(sessionTemplateId, "Transfers"));
        ReturnsApplications();

        await _model.OnGetAsync();

        Assert.False(_model.ShowTemplateFilter);
        Assert.Single(_model.TemplateOptions);
        Assert.True(_model.TemplateOptions[0].Selected);
    }

    [Fact]
    public void BuildPaginationHref_keeps_the_selected_template_and_filters()
    {
        var templateId = Guid.NewGuid();
        _model.SelectedTemplateId = templateId;
        _model.SearchReference = "ABC";

        var href = _model.BuildPaginationHref(2);

        Assert.Contains("currentPage=2", href, StringComparison.Ordinal);
        Assert.Contains("searchReference=ABC", href, StringComparison.Ordinal);
        Assert.Contains($"selectedTemplateId={templateId}", href, StringComparison.Ordinal);
    }

    [Fact]
    public void ClearFiltersHref_keeps_the_selected_template()
    {
        var templateId = Guid.NewGuid();
        _model.SelectedTemplateId = templateId;

        Assert.Equal($"/applications?selectedTemplateId={templateId}", _model.ClearFiltersHref);
    }
}
