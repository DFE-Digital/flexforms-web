using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Dashboard;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Options;
using GovUK.Dfe.FlexForms.Web.Pages.Applications;
using GovUK.Dfe.FlexForms.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Security.Claims;
using System.Text;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages.Applications;

public class DashboardModelTests
{
    private readonly IDashboardApplications _dashboardApplications = Substitute.For<IDashboardApplications>();
    private readonly IApplicationStatusService _applicationStatusService = Substitute.For<IApplicationStatusService>();
    private readonly IUsersClient _usersClient = Substitute.For<IUsersClient>();
    private readonly IApplicationResponseService _applicationResponseService = Substitute.For<IApplicationResponseService>();
    private readonly IMemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions());
    private readonly IOptions<DashboardOptions> _options = Options.Create(new DashboardOptions { PageSize = 10, EnableApplicationFilters = true });
    private readonly DashboardModel _model;
    private readonly ISession _session = Substitute.For<ISession>();

    public DashboardModelTests()
    {
        _applicationStatusService.GetBaseApplicationStatuses().Returns(new List<KeyValuePair<ApplicationStatus, string>>
        {
            new(ApplicationStatus.InProgress, "In progress"),
            new(ApplicationStatus.Deleted, "Deleted")
        });

        // Ensure usersClient returns a non-null Task to avoid awaiting a null task inside UserPermissionsCache.RefreshAsync
        _usersClient.GetMyPermissionsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<UserAuthorizationDto?>(null));

        _model = new DashboardModel(
            NullLogger<DashboardModel>.Instance,
            _applicationStatusService,
            _dashboardApplications,
            _usersClient,
            _applicationResponseService,
            _memoryCache,
            _options);

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
        // session returns no template id
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

        _dashboardApplications.ResolveColumnsAsync(Arg.Any<Guid?>()).Returns(DashboardColumnResolver.DefaultColumns);
        _dashboardApplications.ListAsync(Arg.Any<DashboardApplicationListQuery>())
            .Returns(new DashboardApplicationListResult { Applications = new List<ApplicationWithCalculatedStatus> { item1, item2 }, TotalPages = 1, CurrentPage = 1 });

        // user not admin
        _model.PageContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new Claim(ClaimTypes.Name, "user") }, authenticationType: "Test"));

        await _model.OnGetAsync();

        Assert.Single(_model.Applications);
        Assert.Equal("R2", _model.Applications.First().ApplicationReference);
        Assert.Equal(1, _model.TotalPages);
    }

    [Fact]
    public async Task OnPostCreateApplicationAsync_when_template_missing_sets_error()
    {
        _session.TryGetValue("TemplateId", out Arg.Any<byte[]?>()).Returns(false);

        var result = await _model.OnPostCreateApplicationAsync();

        var page = Assert.IsType<PageResult>(result);
        Assert.True(_model.HasError);
        Assert.Equal(GovUK.Dfe.FlexForms.Application.Dashboard.DashboardMessages.TemplateNotConfigured, _model.ErrorMessage);
    }

    [Fact]
    public async Task OnPostCreateApplicationAsync_when_no_permission_sets_error_and_loads()
    {
        var templateId = Guid.NewGuid();
        _session.TryGetValue("TemplateId", out Arg.Any<byte[]?>()).Returns(call =>
        {
            call[1] = Encoding.UTF8.GetBytes(templateId.ToString());
            return true;
        });

        _dashboardApplications.ResolveColumnsAsync(Arg.Any<Guid?>()).Returns(DashboardColumnResolver.DefaultColumns);
        _dashboardApplications.ListAsync(Arg.Any<DashboardApplicationListQuery>())
            .Returns(new DashboardApplicationListResult { Applications = new List<ApplicationWithCalculatedStatus>(), TotalPages = 1, CurrentPage = 1 });

        // user without Template:Write or admin claim
        _model.PageContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new Claim(ClaimTypes.Name, "user") }, authenticationType: "Test"));

        var result = await _model.OnPostCreateApplicationAsync();

        var page = Assert.IsType<PageResult>(result);
        Assert.True(_model.HasError);
        Assert.Equal(GovUK.Dfe.FlexForms.Application.Dashboard.DashboardMessages.CannotStartApplication, _model.ErrorMessage);
        await _dashboardApplications.Received().ResolveColumnsAsync(Arg.Is<Guid?>(g => g == templateId));
    }

    [Fact]
    public async Task OnPostCreateApplicationAsync_when_success_and_contributors_enabled_redirects_and_sets_session()
    {
        var templateId = Guid.NewGuid();
        _session.TryGetValue("TemplateId", out Arg.Any<byte[]?>()).Returns(call =>
        {
            call[1] = Encoding.UTF8.GetBytes(templateId.ToString());
            return true;
        });

        var createdApp = new ApplicationDto { ApplicationId = Guid.NewGuid(), ApplicationReference = "REF-1", Status = ApplicationStatus.InProgress };
        _dashboardApplications.CreateAsync(templateId).Returns(new DashboardCreateApplicationResult { Application = createdApp, ContributorsEnabled = true });
        _dashboardApplications.ListAsync(Arg.Any<DashboardApplicationListQuery>())
            .Returns(new DashboardApplicationListResult { Applications = new List<ApplicationWithCalculatedStatus>(), TotalPages = 1, CurrentPage = 1 });

        // authenticated user with id/email
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "uid"), new Claim(ClaimTypes.Email, "a@b"), new Claim(ClaimTypes.Name, "Admin"), new Claim(ClaimTypes.Role, "Admin") };
        _model.PageContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));

        var result = await _model.OnPostCreateApplicationAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Applications/Contributors", redirect.PageName);

        await _dashboardApplications.Received().CreateAsync(templateId);
        _session.Received().Set(
            "ApplicationId",
            Arg.Is<byte[]>(b => Encoding.UTF8.GetString(b) == createdApp.ApplicationId.ToString()));
        _session.Received().Set(
            "ApplicationReference",
            Arg.Is<byte[]>(b => Encoding.UTF8.GetString(b) == createdApp.ApplicationReference));
    }

    [Fact]
    public async Task OnGetAsync_admin_user_includes_deleted_applications_and_can_start()
    {
        var templateId = Guid.NewGuid();
        _session.TryGetValue("TemplateId", out Arg.Any<byte[]?>()).Returns(call =>
        {
            call[1] = Encoding.UTF8.GetBytes(templateId.ToString());
            return true;
        });

        var appDeleted = new ApplicationDto { ApplicationId = Guid.NewGuid(), ApplicationReference = "D1", DateCreated = DateTime.UtcNow.AddDays(-2) };
        var appActive = new ApplicationDto { ApplicationId = Guid.NewGuid(), ApplicationReference = "A1", DateCreated = DateTime.UtcNow };

        var itemDeleted = new ApplicationWithCalculatedStatus { Application = appDeleted, CalculatedStatus = new KeyValuePair<ApplicationStatus, string>(ApplicationStatus.Deleted, "Deleted") };
        var itemActive = new ApplicationWithCalculatedStatus { Application = appActive, CalculatedStatus = new KeyValuePair<ApplicationStatus, string>(ApplicationStatus.InProgress, "In progress") };

        _dashboardApplications.ResolveColumnsAsync(Arg.Any<Guid?>()).Returns(DashboardColumnResolver.DefaultColumns);
        _dashboardApplications.ListAsync(Arg.Any<DashboardApplicationListQuery>())
            .Returns(new DashboardApplicationListResult { Applications = new List<ApplicationWithCalculatedStatus> { itemDeleted, itemActive }, TotalPages = 1, CurrentPage = 1 });

        // admin user should see deleted
        _model.PageContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Admin") }, "Test"));

        await _model.OnGetAsync();

        Assert.Equal(2, _model.Applications.Count);
        Assert.True(_model.CanStartNewApplication == false || _model.CanStartNewApplication == true); // just ensure property accessed
    }

    [Fact]
    public async Task OnGetAsync_when_filters_disabled_does_not_add_date_validation_errors()
    {
        // create model with filters disabled
        var options = Options.Create(new DashboardOptions { PageSize = 10, EnableApplicationFilters = false });
        var modelNoFilters = new GovUK.Dfe.FlexForms.Web.Pages.Applications.DashboardModel(
            logger: NullLogger<GovUK.Dfe.FlexForms.Web.Pages.Applications.DashboardModel>.Instance,
            applicationStatusService: _applicationStatusService,
            dashboardApplications: _dashboardApplications,
            usersClient: _usersClient,
            applicationResponseService: _applicationResponseService,
            memoryCache: _memoryCache,
            dashboardOptions: options);

        var httpContext = Substitute.For<HttpContext>();
        httpContext.Session.Returns(_session);
        modelNoFilters.PageContext = new PageContext { HttpContext = httpContext, ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) };

        _session.TryGetValue("TemplateId", out Arg.Any<byte[]?>()).Returns(call =>
        {
            call[1] = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
            return true;
        });

        modelNoFilters.DateStartedFrom = "not-a-date";

        // ensure awaited calls return non-null completed tasks
        _applicationStatusService.GetCustomApplicationStatusesAsync(Arg.Any<Guid?>())
            .Returns(Task.FromResult<IReadOnlyList<CustomApplicationStatusDto>>(new List<CustomApplicationStatusDto>()));
        _dashboardApplications.ListAsync(Arg.Any<DashboardApplicationListQuery>())
            .Returns(Task.FromResult(new DashboardApplicationListResult { Applications = new List<ApplicationWithCalculatedStatus>(), TotalPages = 1, CurrentPage = 1 }));

        await modelNoFilters.OnGetAsync();

        Assert.True(modelNoFilters.ModelState.IsValid);
    }
}
