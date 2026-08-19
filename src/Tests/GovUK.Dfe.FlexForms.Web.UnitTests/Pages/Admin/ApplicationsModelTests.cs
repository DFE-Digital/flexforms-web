using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using AutoFixture.AutoNSubstitute;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Options;
using GovUK.Dfe.FlexForms.Web.Pages.Admin;
using GovUK.Dfe.FlexForms.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.Core.Arguments;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages.Admin;

public class ApplicationsModelTests
{
    private readonly IFixture _fixture;
    private readonly IApplicationsClient _applicationsClient;
    private readonly ITemplateSelectionService _templateSelectionService;
    private readonly ISession _session = Substitute.For<ISession>();
    private readonly ApplicationsModel _model;

    public ApplicationsModelTests()
    {
        _fixture = new Fixture().Customize(new AutoNSubstituteCustomization { ConfigureMembers = true });

        _applicationsClient = Substitute.For<IApplicationsClient>();
        _fixture.Inject(_applicationsClient);

        _templateSelectionService = Substitute.For<ITemplateSelectionService>();
        _fixture.Inject(_templateSelectionService);

        var options = Options.Create(new DashboardOptions { PageSize = 10 });

        _model = new ApplicationsModel(_applicationsClient, _templateSelectionService, options, NullLogger<ApplicationsModel>.Instance);

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
    public async Task OnGetAsync_when_no_selected_template_sets_no_applications()
    {
        _templateSelectionService.GetSelectableTemplatesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<TemplateDto>());

        _model.SelectedTemplateId = null;

        await _model.OnGetAsync(CancellationToken.None);

        Assert.Empty(_model.Applications);
        Assert.False(_model.HasError);
    }

    [Fact]
    public async Task OnGetAsync_when_selected_template_not_found_sets_error()
    {
        _templateSelectionService.GetSelectableTemplatesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<TemplateDto>());

        _model.SelectedTemplateId = Guid.NewGuid();

        await _model.OnGetAsync(CancellationToken.None);

        Assert.True(_model.HasError);
        Assert.Equal("The selected template was not found in this tenant.", _model.ErrorMessage);
        Assert.Null(_model.SelectedTemplateId);
        Assert.Empty(_model.Applications);
    }

    [Fact]
    public async Task OnGetAsync_when_template_found_loads_applications_and_clamps_page()
    {
        var templateId = Guid.NewGuid();
        _model.SelectedTemplateId = templateId;

        _templateSelectionService.GetSelectableTemplatesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<TemplateDto>
            {
                new() { Name = "Test Template", TemplateId = templateId, CreatedOn = DateTime.UtcNow }
            });

        var app1 = new ApplicationDto { ApplicationId = Guid.NewGuid(), ApplicationReference = "R1", DateCreated = DateTime.UtcNow.AddDays(-1) };
        var app2 = new ApplicationDto { ApplicationId = Guid.NewGuid(), ApplicationReference = "R2", DateCreated = DateTime.UtcNow };

        _applicationsClient.GetApplicationsByTemplateAsync(templateId, pageNumber: 1, pageSize: 10, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new PagedResultOfApplicationDto
            {
                Items = new List<ApplicationDto> { app1, app2 },
                TotalPages = 1,
                PageNumber = 1,
                PageSize = 10,
                TotalCount = 2
            });

        _model.CurrentPage = 1;

        await _model.OnGetAsync(CancellationToken.None);

        Assert.Equal(1, _model.TotalPages);
        Assert.Equal(1, _model.CurrentPage);
        // Applications should be ordered by DateCreated descending
        Assert.Equal("R2", _model.Applications.First().ApplicationReference);
    }

    [Fact]
    public async Task OnPostDeleteActionAsync_calls_delete_and_redirects()
    {
        var templateId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();

        var result = await _model.OnPostDeleteActionAsync(templateId, applicationId, CancellationToken.None);

        await _applicationsClient.Received().DeleteApplicationAsync(applicationId);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(templateId, (Guid)redirect.RouteValues![("selectedTemplateId")!]);
        Assert.Equal(1, redirect.RouteValues![("CurrentPage")!]);
    }
}
