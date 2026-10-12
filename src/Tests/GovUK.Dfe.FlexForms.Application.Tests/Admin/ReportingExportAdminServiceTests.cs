using System.Collections.ObjectModel;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.CoreLibs.Http.Models;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Application.Tests.Admin;

public class ReportingExportAdminServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid TemplateId = Guid.Parse("33333333-3333-4333-8333-333333333333");

    private readonly IReportingExportClient _client = Substitute.For<IReportingExportClient>();
    private readonly ITemplatesClient _templates = Substitute.For<ITemplatesClient>();
    private readonly ReportingExportAdminService _service;

    public ReportingExportAdminServiceTests()
    {
        _templates.GetAccessibleTemplatesAsync(Arg.Any<CancellationToken>()).Returns(new ObservableCollection<TemplateDto>());
        _service = new ReportingExportAdminService(_client, _templates, NullLogger<ReportingExportAdminService>.Instance);
    }

    [Fact]
    public async Task LoadAsync_ShouldLoadTenantDefaultOnly_WhenNoTemplateSelected()
    {
        var tenantDefault = Default(null, ReportingExportModeSetting.ApproveFirst);
        _client.GetTenantReportingExportDefaultAsync(TenantId, Arg.Any<CancellationToken>()).Returns(tenantDefault);
        var state = new ReportingExportWorkState { TenantId = TenantId };

        await _service.LoadAsync(state);

        Assert.Same(tenantDefault, state.TenantDefault);
        Assert.False(state.HasError);
        await _client.DidNotReceiveWithAnyArgs().GetTemplateReportingExportPolicyAsync(default, default, default);
    }

    [Fact]
    public async Task LoadAsync_ShouldLoadTemplatePolicyAndRefresh_WhenSelected()
    {
        var refreshId = Guid.NewGuid();
        var policy = new ReportingExportPolicyDto(TemplateId, 4, ReportingExportMode.ApproveFirst, ReportingExportDefaultSource.Tenant, []);
        _client.GetTemplateReportingExportPolicyAsync(TenantId, TemplateId, Arg.Any<CancellationToken>()).Returns(policy);
        _client.GetTemplateReportingExportDefaultAsync(TenantId, TemplateId, Arg.Any<CancellationToken>())
            .Returns(Default(TemplateId, ReportingExportModeSetting.Inherit));
        _client.GetReportingExportRefreshAsync(TenantId, refreshId, Arg.Any<CancellationToken>())
            .Returns(new ReportingExportRefreshDto(refreshId, ReportingExportRefreshStatus.Running, 3, 1, null, DateTime.UtcNow, null, null));
        var state = new ReportingExportWorkState { TenantId = TenantId, SelectedTemplateId = TemplateId, RefreshId = refreshId };

        await _service.LoadAsync(state);

        Assert.Same(policy, state.Policy);
        Assert.Equal(ReportingExportModeSetting.Inherit, state.TemplateDefault!.Mode);
        Assert.Equal(ReportingExportRefreshStatus.Running, state.Refresh!.Status);
    }

    [Fact]
    public async Task LoadAsync_ShouldExplain_WhenTemplateNotKnownToReporting()
    {
        _client.GetTemplateReportingExportDefaultAsync(TenantId, TemplateId, Arg.Any<CancellationToken>())
            .Returns(Default(TemplateId, ReportingExportModeSetting.Inherit));
        _client.GetTemplateReportingExportPolicyAsync(TenantId, TemplateId, Arg.Any<CancellationToken>())
            .ThrowsAsync(ApiError(404, "This template's fields are not known to reporting yet."));
        var state = new ReportingExportWorkState { TenantId = TenantId, SelectedTemplateId = TemplateId };

        await _service.LoadAsync(state);

        Assert.False(state.HasError);
        Assert.Equal("This template's fields are not known to reporting yet.", state.PolicyMessage);
    }

    [Fact]
    public async Task LoadAsync_ShouldReportError_WhenReportingUnavailable()
    {
        _client.GetTenantReportingExportDefaultAsync(TenantId, Arg.Any<CancellationToken>())
            .ThrowsAsync(ApiError(400, "Reporting export is not set up in this environment."));
        var state = new ReportingExportWorkState { TenantId = TenantId, SelectedTemplateId = TemplateId };

        await _service.LoadAsync(state);

        Assert.True(state.HasError);
        Assert.Equal("Reporting export is not set up in this environment.", state.ErrorMessage);
        await _client.DidNotReceiveWithAnyArgs().GetTemplateReportingExportPolicyAsync(default, default, default);
    }

    [Fact]
    public async Task SetTenantDefaultAsync_ShouldRequireReason_ForExportAll()
    {
        var outcome = await _service.SetTenantDefaultAsync(new ReportingExportWorkState { TenantId = TenantId }, ReportingExportModeSetting.ExportAll, "  ");

        Assert.Equal(ReportingExportMessages.ReasonRequiredForExportAll, outcome.ErrorMessage);
        await _client.DidNotReceiveWithAnyArgs().UpdateTenantReportingExportDefaultAsync(default, default!, default);
    }

    [Fact]
    public async Task SetTenantDefaultAsync_ShouldRedirectWithRefresh_WhenApplied()
    {
        var refreshId = Guid.NewGuid();
        _client.UpdateTenantReportingExportDefaultAsync(TenantId, Arg.Any<UpdateReportingExportDefaultRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ReportingExportChangeResultDto(ReportingExportChangeStatus.Applied, 5, 1, [], refreshId));
        var state = new ReportingExportWorkState { TenantId = TenantId, SelectedTemplateId = TemplateId };

        var outcome = await _service.SetTenantDefaultAsync(state, ReportingExportModeSetting.ExportAll, " DPIA 42 ");

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, outcome.Kind);
        Assert.Equal(ReportingExportMessages.Saved, outcome.SuccessMessage);
        Assert.Equal(refreshId.ToString(), outcome.RouteValues[ReportingExportAdminService.RefreshRouteKey]);
        Assert.Equal(TemplateId.ToString(), outcome.RouteValues[ReportingExportAdminService.SelectedTemplateRouteKey]);
        await _client.Received(1).UpdateTenantReportingExportDefaultAsync(
            TenantId,
            Arg.Is<UpdateReportingExportDefaultRequest>(r => r.Mode == ReportingExportModeSetting.ExportAll && r.Reason == "DPIA 42"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetTemplateDefaultAsync_ShouldRequireTemplate()
    {
        var outcome = await _service.SetTemplateDefaultAsync(new ReportingExportWorkState { TenantId = TenantId }, ReportingExportModeSetting.Inherit, null);

        Assert.Equal(ReportingExportMessages.SelectTemplate, outcome.ErrorMessage);
    }

    [Fact]
    public async Task DecideAsync_ShouldSendSelectedFields()
    {
        _client.UpdateTemplateReportingExportDecisionsAsync(TenantId, TemplateId, Arg.Any<UpdateReportingExportDecisionsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ReportingExportChangeResultDto(ReportingExportChangeStatus.Applied, 6, 2, ["Email may be personal data."], Guid.NewGuid()));
        var state = new ReportingExportWorkState { TenantId = TenantId, SelectedTemplateId = TemplateId };
        var keys = new[]
        {
            ReportingExportAdminService.FieldKey(null, "email"),
            ReportingExportAdminService.FieldKey("contacts", "name|first"),
            "not-a-key",
        };

        var outcome = await _service.DecideAsync(state, keys, ReportingExportDecision.Allowed, "Needed for monitoring");

        Assert.Equal($"{ReportingExportMessages.Saved} Email may be personal data.", outcome.SuccessMessage);
        await _client.Received(1).UpdateTemplateReportingExportDecisionsAsync(
            TenantId,
            TemplateId,
            Arg.Is<UpdateReportingExportDecisionsRequest>(r =>
                r.Decisions.Count == 2
                && r.Decisions.Any(d => d.ParentFieldId == "" && d.FieldId == "email")
                && r.Decisions.Any(d => d.ParentFieldId == "contacts" && d.FieldId == "name|first")
                && r.Decisions.All(d => d.Decision == ReportingExportDecision.Allowed && d.Reason == "Needed for monitoring")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DecideAsync_ShouldRequireFields()
    {
        var state = new ReportingExportWorkState { TenantId = TenantId, SelectedTemplateId = TemplateId };

        var outcome = await _service.DecideAsync(state, [], ReportingExportDecision.Denied, null);

        Assert.Equal(ReportingExportMessages.SelectFields, outcome.ErrorMessage);
        await _client.DidNotReceiveWithAnyArgs().UpdateTemplateReportingExportDecisionsAsync(default, default, default!, default);
    }

    [Fact]
    public async Task DecideAsync_ShouldShowApiMessage_WhenRejected()
    {
        _client.UpdateTemplateReportingExportDecisionsAsync(TenantId, TemplateId, Arg.Any<UpdateReportingExportDecisionsRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(ApiError(400, "These fields are not in the template: email."));
        var state = new ReportingExportWorkState { TenantId = TenantId, SelectedTemplateId = TemplateId };

        var outcome = await _service.DecideAsync(state, [ReportingExportAdminService.FieldKey(null, "email")], ReportingExportDecision.Denied, null);

        Assert.Equal("These fields are not in the template: email.", outcome.ErrorMessage);
        Assert.Null(outcome.SuccessMessage);
    }

    [Theory]
    [InlineData(null, "email")]
    [InlineData("", "email")]
    [InlineData("parent/with+symbols", "fïeld\twith=odd chars")]
    public void FieldKey_ShouldRoundTrip(string? parent, string field)
    {
        var parsed = ReportingExportAdminService.ParseFieldKey(ReportingExportAdminService.FieldKey(parent, field));

        Assert.Equal((parent ?? "", field), parsed);
    }

    private static ReportingExportDefaultDto Default(Guid? templateId, ReportingExportModeSetting mode) =>
        new(templateId, mode, ReportingExportMode.ApproveFirst, ReportingExportDefaultSource.BuiltIn, null, null, null);

    private static ExternalApplicationsException<ExceptionResponse> ApiError(int status, string message) =>
        new(message, status, "body", new Dictionary<string, IEnumerable<string>>(), new ExceptionResponse { StatusCode = status, Message = message }, null);
}
