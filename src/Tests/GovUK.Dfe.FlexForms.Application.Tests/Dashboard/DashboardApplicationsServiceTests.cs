using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Dashboard;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Application.Tests.Dashboard;

public class DashboardApplicationsServiceTests
{
    private readonly IApplicationsClient _applications = Substitute.For<IApplicationsClient>();
    private readonly IFormTemplateProvider _templates = Substitute.For<IFormTemplateProvider>();
    private readonly IContributorPatternService _contributors = Substitute.For<IContributorPatternService>();
    private readonly DashboardApplicationsService _service;

    public DashboardApplicationsServiceTests()
    {
        _service = new DashboardApplicationsService(
            _applications,
            _templates,
            _contributors,
            NullLogger<DashboardApplicationsService>.Instance);
    }

    [Fact]
    public async Task ResolveColumnsAsync_ShouldReturnDefaults_WhenTemplateIdIsMissing()
    {
        var columns = await _service.ResolveColumnsAsync(null);

        Assert.Equal(DashboardColumnResolver.DefaultColumns, columns);
    }

    [Fact]
    public async Task ResolveColumnsAsync_ShouldResolveFromTemplate_WhenTemplateLoads()
    {
        var templateId = Guid.NewGuid();
        _templates.GetTemplateAsync(templateId.ToString(), Arg.Any<CancellationToken>())
            .Returns(new FormTemplate
            {
                TemplateId = templateId.ToString(),
                TemplateName = "Transfers",
                Description = "desc",
                TaskGroups = []
            });

        var columns = await _service.ResolveColumnsAsync(templateId);

        Assert.Equal(DashboardColumnResolver.DefaultColumns.Count, columns.Count);
    }

    [Fact]
    public async Task ListAsync_ShouldQueryMyApplications_WhenScopeIsMine()
    {
        var templateId = Guid.NewGuid();
        var app = new ApplicationDto
        {
            ApplicationId = Guid.NewGuid(),
            ApplicationReference = "REF-1",
            DateCreated = DateTime.UtcNow
        };
        _applications.GetMyApplicationsAsync(
                Arg.Any<Guid?>(),
                Arg.Any<bool?>(),
                Arg.Any<int?>(),
                Arg.Any<int?>(),
                Arg.Any<string>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<ApplicationStatus?>(),
                Arg.Any<CancellationToken>())
            .Returns(new PagedResultOfApplicationDto
            {
                Items = [app],
                TotalPages = 2,
                PageNumber = 1,
                PageSize = 50,
                TotalCount = 2
            });

        var result = await _service.ListAsync(new DashboardApplicationListQuery
        {
            TemplateId = templateId,
            CurrentPage = 9,
            PageSize = 50,
            Scope = DashboardApplicationListScope.Mine
        });

        Assert.Equal(2, result.TotalPages);
        Assert.Equal(2, result.CurrentPage);
        Assert.Equal("REF-1", Assert.Single(result.Applications).ApplicationReference);
        await _applications.DidNotReceive().GetApplicationsByTemplateAsync(
            Arg.Any<Guid>(),
            Arg.Any<bool?>(),
            Arg.Any<int?>(),
            Arg.Any<int?>(),
            Arg.Any<string>(),
            Arg.Any<DateTime?>(),
            Arg.Any<DateTime?>(),
            Arg.Any<DateTime?>(),
            Arg.Any<DateTime?>(),
            Arg.Any<ApplicationStatus?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListAsync_ShouldQueryByTemplate_WhenScopeIsAllForTemplate()
    {
        var templateId = Guid.NewGuid();
        _applications.GetApplicationsByTemplateAsync(
                Arg.Any<Guid>(),
                Arg.Any<bool?>(),
                Arg.Any<int?>(),
                Arg.Any<int?>(),
                Arg.Any<string>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<ApplicationStatus?>(),
                Arg.Any<CancellationToken>())
            .Returns(new PagedResultOfApplicationDto
            {
                Items = [],
                TotalPages = 1
            });

        var result = await _service.ListAsync(new DashboardApplicationListQuery
        {
            TemplateId = templateId,
            Scope = DashboardApplicationListScope.AllForTemplate
        });

        Assert.Empty(result.Applications);
        await _applications.Received(1).GetApplicationsByTemplateAsync(
            templateId,
            Arg.Any<bool?>(),
            Arg.Any<int?>(),
            Arg.Any<int?>(),
            Arg.Any<string>(),
            Arg.Any<DateTime?>(),
            Arg.Any<DateTime?>(),
            Arg.Any<DateTime?>(),
            Arg.Any<DateTime?>(),
            Arg.Any<ApplicationStatus?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnContributorsFlag_WhenPatternIsEnabled()
    {
        var templateId = Guid.NewGuid();
        var created = new ApplicationDto
        {
            ApplicationId = Guid.NewGuid(),
            ApplicationReference = "REF-9",
            Status = ApplicationStatus.InProgress
        };
        _applications.CreateApplicationAsync(Arg.Any<CreateApplicationRequest>(), Arg.Any<CancellationToken>())
            .Returns(created);
        _contributors.IsEnabledAsync(templateId.ToString(), Arg.Any<ApplicationDto?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _service.CreateAsync(templateId);

        Assert.Same(created, result.Application);
        Assert.True(result.ContributorsEnabled);
        await _applications.Received(1).CreateApplicationAsync(
            Arg.Is<CreateApplicationRequest>(r => r.TemplateId == templateId && r.InitialResponseBody == "{}"),
            Arg.Any<CancellationToken>());
    }
}
