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
using System.Text;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages.Applications;

public class IndexModelTests
{
    private readonly IDashboardApplications _dashboardApplications = Substitute.For<IDashboardApplications>();
    private readonly IApplicationStatusService _applicationStatusService = Substitute.For<IApplicationStatusService>();
    private readonly IOptions<DashboardOptions> _options = Options.Create(new DashboardOptions { PageSize = 10, EnableApplicationFilters = true });
    private readonly IndexModel _model;
    private readonly ISession _session = Substitute.For<ISession>();

    public IndexModelTests()
    {
        _applicationStatusService.GetBaseApplicationStatuses().Returns(new List<KeyValuePair<ApplicationStatus, string>>
        {
            new(ApplicationStatus.InProgress, "In progress"),
            new(ApplicationStatus.Deleted, "Deleted")
        });

        _model = new IndexModel(_dashboardApplications, _applicationStatusService, _options, NullLogger<IndexModel>.Instance);

        var httpContext = Substitute.For<HttpContext>();
        httpContext.Session.Returns(_session);
        _model.PageContext = new PageContext
        {
            HttpContext = httpContext,
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        };
        _model.TempData = new TempDataDictionary(httpContext, Substitute.For<ITempDataProvider>());
    }

    [Fact]
    public async Task OnGetAsync_when_template_missing_sets_no_applications()
    {
        _session.TryGetValue("TemplateId", out Arg.Any<byte[]?>()).Returns(false);

        await _model.OnGetAsync();

        Assert.Empty(_model.Applications);
    }

    [Fact]
    public async Task OnGetAsync_with_template_filters_deleted_applications_when_not_admin()
    {
        var templateId = Guid.NewGuid();
        _session.TryGetValue("TemplateId", out Arg.Any<byte[]?>()).Returns(call =>
        {
            call[1] = Encoding.UTF8.GetBytes(templateId.ToString());
            return true;
        });

        var app1 = new ApplicationDto { ApplicationId = Guid.NewGuid(), ApplicationReference = "R1", DateCreated = DateTime.UtcNow.AddDays(-1) };
        var app2 = new ApplicationDto { ApplicationId = Guid.NewGuid(), ApplicationReference = "R2", DateCreated = DateTime.UtcNow };

        var item1 = new ApplicationWithCalculatedStatus { Application = app1, CalculatedStatus = new KeyValuePair<ApplicationStatus, string>(ApplicationStatus.Deleted, "Deleted") };
        var item2 = new ApplicationWithCalculatedStatus { Application = app2, CalculatedStatus = new KeyValuePair<ApplicationStatus, string>(ApplicationStatus.InProgress, "In progress") };

        _dashboardApplications.ListAsync(Arg.Any<DashboardApplicationListQuery>())
            .Returns(new DashboardApplicationListResult { Applications = new List<ApplicationWithCalculatedStatus> { item1, item2 }, TotalPages = 1, CurrentPage = 1 });

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
        var templateId = Guid.NewGuid();
        _session.TryGetValue("TemplateId", out Arg.Any<byte[]?>()).Returns(call =>
        {
            call[1] = Encoding.UTF8.GetBytes(templateId.ToString());
            return true;
        });

        _model.DateStartedFrom = "not-a-date";

        // ensure dashboardApplications would return something if called
        _dashboardApplications.ListAsync(Arg.Any<DashboardApplicationListQuery>())
            .Returns(new DashboardApplicationListResult { Applications = new List<ApplicationWithCalculatedStatus> { }, TotalPages = 1, CurrentPage = 1 });

        await _model.OnGetAsync();

        Assert.False(_model.ModelState.IsValid);
        Assert.Contains("DateStartedFrom", _model.ModelState);
        Assert.Empty(_model.Applications);
    }

    [Fact]
    public async Task OnGetAsync_admin_sees_deleted_status_in_status_filters()
    {
        // provide template
        var templateId = Guid.NewGuid();
        _session.TryGetValue("TemplateId", out Arg.Any<byte[]?>()).Returns(call =>
        {
            call[1] = Encoding.UTF8.GetBytes(templateId.ToString());
            return true;
        });

        // admin user
        _model.PageContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Admin") }, "Test"));

        _applicationStatusService.GetCustomApplicationStatusesAsync(Arg.Any<Guid?>())
            .Returns(new List<CustomApplicationStatusDto>());

        _dashboardApplications.ListAsync(Arg.Any<DashboardApplicationListQuery>())
            .Returns(new DashboardApplicationListResult { Applications = new List<ApplicationWithCalculatedStatus>(), TotalPages = 1, CurrentPage = 1 });

        await _model.OnGetAsync();

        Assert.Contains(_model.StatusFilters, s => s.Key == ApplicationStatus.Deleted);
    }

    [Fact]
    public async Task OnGetAsync_when_filters_disabled_loads_applications_without_validation()
    {
        var optionsNoFilters = Options.Create(new DashboardOptions { PageSize = 10, EnableApplicationFilters = false });
        var modelNoFilters = new IndexModel(_dashboardApplications, _applicationStatusService, optionsNoFilters, NullLogger<IndexModel>.Instance);

        var httpContext = Substitute.For<HttpContext>();
        httpContext.Session.Returns(_session);
        modelNoFilters.PageContext = new PageContext { HttpContext = httpContext, ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) };
        modelNoFilters.TempData = new TempDataDictionary(httpContext, Substitute.For<ITempDataProvider>());

        _session.TryGetValue("TemplateId", out Arg.Any<byte[]?>()).Returns(call =>
        {
            call[1] = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
            return true;
        });

        _dashboardApplications.ListAsync(Arg.Any<DashboardApplicationListQuery>())
            .Returns(new DashboardApplicationListResult { Applications = new List<ApplicationWithCalculatedStatus>(), TotalPages = 1, CurrentPage = 1 });

        modelNoFilters.DateStartedFrom = "not-a-date";

        await modelNoFilters.OnGetAsync();

        Assert.True(modelNoFilters.ModelState.IsValid);
        await _dashboardApplications.Received().ListAsync(Arg.Any<DashboardApplicationListQuery>());
    }
}
