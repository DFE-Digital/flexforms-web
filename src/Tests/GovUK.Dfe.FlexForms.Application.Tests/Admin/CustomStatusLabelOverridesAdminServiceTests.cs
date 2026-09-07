using System.Collections.ObjectModel;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Application.Tests.Admin;

public class CustomStatusLabelOverridesAdminServiceTests
{
    private readonly IFormTemplateProvider _templatesProvider = Substitute.For<IFormTemplateProvider>();
    private readonly ITemplatesClient _templates = Substitute.For<ITemplatesClient>();
    private readonly CustomStatusLabelOverridesAdminService _service;

    public CustomStatusLabelOverridesAdminServiceTests()
    {
        _service = new CustomStatusLabelOverridesAdminService(
            _templatesProvider,
            _templates,
            NullLogger<CustomStatusLabelOverridesAdminService>.Instance);
    }

    [Fact]
    public async Task LoadAvailableTemplatesAsync_ShouldOrderLiveFirst_WhenTemplatesExist()
    {
        var live = new TemplateDto
        {
            TemplateId = Guid.NewGuid(),
            Name = "Zed",
            CreatedOn = DateTime.UtcNow,
            IsLive = true
        };
        var draft = new TemplateDto
        {
            TemplateId = Guid.NewGuid(),
            Name = "Alpha",
            CreatedOn = DateTime.UtcNow,
            IsLive = false
        };
        _templates.GetAccessibleTemplatesAsync(Arg.Any<CancellationToken>()).Returns([draft, live]);
        var state = new CustomStatusLabelOverridesWorkState();

        await _service.LoadAvailableTemplatesAsync(state);

        Assert.Equal(new[] { live.TemplateId, draft.TemplateId }, state.AvailableTemplates.Select(t => t.TemplateId));
    }

    [Fact]
    public async Task LoadTemplateDataAsync_ShouldPopulateTemplate_WhenApiSucceeds()
    {
        var templateId = Guid.NewGuid();
        var formTemplate = new FormTemplate
        {
            TemplateId = templateId.ToString(),
            TemplateName = "Transfers",
            Description = "desc",
            TaskGroups = []
        };
        _templates.GetLatestTemplateSchemaAsync(templateId, Arg.Any<CancellationToken>())
            .Returns(new TemplateSchemaDto
            {
                TemplateId = templateId,
                TemplateVersionId = Guid.NewGuid(),
                VersionNumber = "1.0.0",
                JsonSchema = "{}"
            });
        _templatesProvider.GetTemplateAsync(templateId.ToString(), Arg.Any<CancellationToken>()).Returns(formTemplate);
        var state = new CustomStatusLabelOverridesWorkState();

        await _service.LoadTemplateDataAsync(state, templateId);

        Assert.Equal("1.0.0", state.CurrentVersionNumber);
        Assert.Same(formTemplate, state.CurrentTemplate);
    }

    [Fact]
    public async Task OverrideAsync_ShouldCreateCustomStatus_WhenCalled()
    {
        var templateId = Guid.NewGuid();

        await _service.OverrideAsync(templateId, ApplicationStatus.InProgress, "Working");

        await _templates.Received(1).CreateCustomApplicationStatusAsync(
            templateId,
            Arg.Is<CustomApplicationStatusRequest>(r =>
                r.ApplicationStatus == ApplicationStatus.InProgress && r.Label == "Working"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadAvailableTemplatesAsync_ShouldReturnEmpty_WhenApiFails()
    {
        _templates.GetAccessibleTemplatesAsync(Arg.Any<CancellationToken>())
            .Returns<ObservableCollection<TemplateDto>>(_ => throw new InvalidOperationException("down"));
        var state = new CustomStatusLabelOverridesWorkState();

        await _service.LoadAvailableTemplatesAsync(state);

        Assert.Empty(state.AvailableTemplates);
    }

    [Fact]
    public async Task LoadStatusOverrideAsync_ShouldUseCustomLabel_WhenOverrideExists()
    {
        var templateId = Guid.NewGuid();
        var state = new CustomStatusLabelOverridesWorkState();
        _templates.GetCustomApplicationStatusesAsync(templateId, Arg.Any<CancellationToken>())
            .Returns([
                new CustomApplicationStatusDto
                {
                    ApplicationStatus = ApplicationStatus.InProgress,
                    Label = "Working"
                }
            ]);

        await _service.LoadStatusOverrideAsync(state, templateId, ApplicationStatus.InProgress);

        Assert.Equal("Working", state.BaseStatusOverrideValue);
        Assert.NotEmpty(state.BaseStatuses);
    }

    [Fact]
    public void PopulateBaseStatuses_ShouldIncludeAllStatuses()
    {
        var state = new CustomStatusLabelOverridesWorkState();

        _service.PopulateBaseStatuses(state);

        Assert.Equal(Enum.GetValues<ApplicationStatus>().Length, state.BaseStatuses.Count);
    }
}
