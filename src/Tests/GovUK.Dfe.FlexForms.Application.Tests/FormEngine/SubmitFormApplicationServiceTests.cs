using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Task = System.Threading.Tasks.Task;
using TaskModel = GovUK.Dfe.FlexForms.Domain.Models.Task;

namespace GovUK.Dfe.FlexForms.Application.Tests.FormEngine;

public class SubmitFormApplicationServiceTests
{
    private readonly IApplicationStateService _stateService = Substitute.For<IApplicationStateService>();
    private readonly IApplicationsClient _applications = Substitute.For<IApplicationsClient>();
    private readonly IFormSessionStore _session = Substitute.For<IFormSessionStore>();
    private readonly IConditionalLogicOrchestrator _conditional = Substitute.For<IConditionalLogicOrchestrator>();
    private readonly SubmitFormApplicationService _service;

    public SubmitFormApplicationServiceTests()
    {
        _conditional.ApplyConditionalLogicAsync(default!, default!, default)
            .ReturnsForAnyArgs(new FormConditionalState());
        _stateService.ValidateAllRequiredFieldsForSubmission(default!, default!, default)
            .ReturnsForAnyArgs(new Dictionary<string, List<string>>());
        _service = new SubmitFormApplicationService(
            _stateService,
            _applications,
            _session,
            _conditional,
            NullLogger<SubmitFormApplicationService>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStay_WhenNotEditable()
    {
        var result = await _service.ExecuteAsync(State(isEditable: false));
        Assert.Contains(result.Errors, e => e.Message == FormEngineMessages.NoWritePermission);
        await _applications.DidNotReceive().SubmitApplicationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStay_WhenTasksIncomplete()
    {
        _stateService.AreAllTasksCompleted(default!, default!, default, default).ReturnsForAnyArgs(false);
        var result = await _service.ExecuteAsync(State());
        Assert.Contains(result.Errors, e => e.Message == FormEngineMessages.AllSectionsMustBeCompleted);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStay_WhenRequiredFieldsMissing()
    {
        _stateService.AreAllTasksCompleted(default!, default!, default, default).ReturnsForAnyArgs(true);
        _stateService.ValidateAllRequiredFieldsForSubmission(default!, default!, default)
            .ReturnsForAnyArgs(new Dictionary<string, List<string>> { ["t1"] = ["name"] });
        var result = await _service.ExecuteAsync(State());
        Assert.Contains(result.Errors, e => e.Message.Contains("missing required information"));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStay_WhenApplicationIdMissing()
    {
        _stateService.AreAllTasksCompleted(default!, default!, default, default).ReturnsForAnyArgs(true);
        var state = State();
        state.ApplicationId = null;
        var result = await _service.ExecuteAsync(state);
        Assert.Contains(result.Errors, e => e.Message == FormEngineMessages.ApplicationNotFound);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStay_WhenFileGateBlocksSubmit()
    {
        _stateService.AreAllTasksCompleted(default!, default!, default, default).ReturnsForAnyArgs(true);
        var state = State();
        _applications.GetFileValidationGateAsync(state.ApplicationId!.Value, Arg.Any<CancellationToken>())
            .Returns(new FileValidationGateDto
            {
                CanSubmit = false,
                BlockingFiles = [new FileValidationBlockDto { OriginalFileName = "bad.pdf" }]
            });

        var result = await _service.ExecuteAsync(state);

        Assert.True(result.FileValidationBlocksSubmit);
        Assert.Contains(result.Errors, e => e.Message.Contains("bad.pdf"));
        await _applications.DidNotReceive().SubmitApplicationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSubmit_WhenGatesPass()
    {
        _stateService.AreAllTasksCompleted(default!, default!, default, default).ReturnsForAnyArgs(true);
        var state = State();
        _applications.GetFileValidationGateAsync(state.ApplicationId!.Value, Arg.Any<CancellationToken>())
            .Returns(new FileValidationGateDto { CanSubmit = true, BlockingFiles = [] });
        _applications.SubmitApplicationAsync(state.ApplicationId.Value, Arg.Any<CancellationToken>())
            .Returns(new ApplicationDto { ApplicationId = state.ApplicationId.Value, Status = ApplicationStatus.Submitted });

        var result = await _service.ExecuteAsync(state);

        Assert.Equal(FormEngineOutcomeKind.RedirectToPage, result.Kind);
        _session.Received().SetString($"ApplicationStatus_{state.ApplicationId}", Arg.Any<string>());
    }

    private static FormEngineWorkState State(bool isEditable = true) =>
        new()
        {
            ReferenceNumber = "REF-1",
            ApplicationId = Guid.NewGuid(),
            IsEditable = isEditable,
            ApplicationStatus = "InProgress",
            Template = new FormTemplate
            {
                TemplateId = "tpl",
                TemplateName = "tpl",
                Description = "tpl",
                TaskGroups =
                [
                    new TaskGroup
                    {
                        GroupId = "g1",
                        GroupName = "g",
                        GroupOrder = 1,
                        GroupStatus = "NotStarted",
                        Tasks = [new TaskModel { TaskId = "t1", TaskName = "About you", TaskOrder = 1, TaskStatusString = "NotStarted", Pages = [] }]
                    }
                ]
            },
            FormData = new Dictionary<string, object>(),
            Data = new Dictionary<string, object>()
        };
}
