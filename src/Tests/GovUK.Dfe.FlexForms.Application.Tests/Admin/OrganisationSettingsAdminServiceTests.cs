using System.Text;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Application.Options;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Application.Tests.Admin;

public class OrganisationSettingsAdminServiceTests
{
    private readonly ITenantAdminClient _client = Substitute.For<ITenantAdminClient>();
    private readonly ITemplatesClient _templates = Substitute.For<ITemplatesClient>();
    private readonly OrganisationSettingsAdminService _service;
    private readonly OrganisationSettingsWorkState _state = new()
    {
        TenantId = Guid.NewGuid(),
        TenantName = "Transfers",
        TerminologySingular = "plan",
        TerminologyPlural = "plans",
        BannerEnabled = true,
        BannerHeading = "Notice",
        BannerMessage = "Hello",
        DashboardPageSize = 25,
        DashboardEnableFilters = true
    };

    public OrganisationSettingsAdminServiceTests()
    {
        _templates.GetAccessibleTemplatesAsync(Arg.Any<CancellationToken>()).Returns([]);
        _service = new OrganisationSettingsAdminService(
            _client,
            _templates,
            NullLogger<OrganisationSettingsAdminService>.Instance);
    }

    [Fact]
    public async Task LoadAsync_ShouldApplySettings_WhenApiReturnsJson()
    {
        _client.GetSafeTenantSettingsAsync(_state.TenantId, Arg.Any<CancellationToken>())
            .Returns(new GetTenantSettingsResponse(
                _state.TenantId,
                "Loaded tenant",
                [
                    new TenantSettingDto(
                        Guid.NewGuid(),
                        "ApplicationTerminology",
                        "Web",
                        """{"Singular":"reform","Plural":"reforms"}""",
                        false,
                        DateTime.UtcNow),
                    new TenantSettingDto(
                        Guid.NewGuid(),
                        "Dashboard",
                        "Web",
                        """{"PageSize":10,"EnableApplicationFilters":false,"MainHeading":"Your visits","InProgressHeading":"Visits in progress","StartNewHeading":"Start a new visit","StartNewHint":"Hint text","StartNewButtonText":"Start new visit"}""",
                        false,
                        DateTime.UtcNow),
                    new TenantSettingDto(
                        Guid.NewGuid(),
                        "ApplicationPreview",
                        "Web",
                        """{"PageHeading":"Review your visit","SubmitHeading":"Submit your visit","SubmitHint":"Please confirm","SubmitButtonText":"Send","HideSubmitSection":true}""",
                        false,
                        DateTime.UtcNow),
                    new TenantSettingDto(
                        Guid.NewGuid(),
                        "ApplicationSubmittedPage",
                        "Web",
                        """{"_default":{"PanelTitle":"Plan submitted","BodyMarkdown":"Thanks."}}""",
                        false,
                        DateTime.UtcNow)
                ]));

        await _service.LoadAsync(_state);

        Assert.Equal("Loaded tenant", _state.TenantName);
        Assert.Equal("reform", _state.TerminologySingular);
        Assert.Equal("reforms", _state.TerminologyPlural);
        Assert.Equal(10, _state.DashboardPageSize);
        Assert.False(_state.DashboardEnableFilters);
        Assert.Equal("Your visits", _state.DashboardMainHeading);
        Assert.Equal("Visits in progress", _state.DashboardInProgressHeading);
        Assert.Equal("Start a new visit", _state.DashboardStartNewHeading);
        Assert.Equal("Hint text", _state.DashboardStartNewHint);
        Assert.Equal("Start new visit", _state.DashboardStartNewButtonText);
        Assert.Equal("Review your visit", _state.PreviewPageHeading);
        Assert.Equal("Submit your visit", _state.PreviewSubmitHeading);
        Assert.Equal("Please confirm", _state.PreviewSubmitHint);
        Assert.Equal("Send", _state.PreviewSubmitButtonText);
        Assert.True(_state.PreviewHideSubmitSection);
        Assert.Equal("_default", _state.SubmittedTemplateId);
        Assert.Equal("Plan submitted", _state.SubmittedPanelTitle);
        Assert.Equal("Thanks.", _state.SubmittedBodyMarkdown);
        Assert.False(_state.HasError);
    }

    [Fact]
    public async Task LoadAsync_ShouldSetError_WhenApiFails()
    {
        _client.GetSafeTenantSettingsAsync(_state.TenantId, Arg.Any<CancellationToken>())
            .Throws(new ExternalApplicationsException("boom", 500, "err", null!, null!));

        await _service.LoadAsync(_state);

        Assert.True(_state.HasError);
        Assert.Equal(OrganisationSettingsMessages.LoadFailed + " (HTTP 500)", _state.ErrorMessage);
    }

    [Fact]
    public async Task SaveAsync_ShouldUpsertAndRefresh_WhenInputIsValid()
    {
        var result = await _service.SaveAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal(OrganisationSettingsMessages.Saved, result.SuccessMessage);
        Assert.True(result.RefreshLocalCaches);
        await _client.Received(5).UpsertSafeTenantSettingAsync(
            _state.TenantId,
            Arg.Any<UpsertTenantSettingRequest>(),
            Arg.Any<CancellationToken>());
        await _client.Received(1).RefreshTenantConfigurationAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_ShouldStay_WhenApiFails()
    {
        _client.UpsertSafeTenantSettingAsync(
                Arg.Any<Guid>(),
                Arg.Any<UpsertTenantSettingRequest>(),
                Arg.Any<CancellationToken>())
            .Throws(new ExternalApplicationsException("boom", 403, "err", null!, null!));

        var result = await _service.SaveAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.StayOnPage, result.Kind);
        Assert.Equal(OrganisationSettingsMessages.SaveFailed + " (HTTP 403)", result.ErrorMessage);
        Assert.DoesNotContain("WAF", result.ErrorMessage);
    }

    [Fact]
    public async Task SaveAsync_ShouldMergeSelectedSubmittedPageCopy()
    {
        var templateId = Guid.NewGuid();
        _templates.GetAccessibleTemplatesAsync(Arg.Any<CancellationToken>()).Returns([
            new TemplateDto { TemplateId = templateId, Name = "Transfers", CreatedOn = DateTime.UtcNow }
        ]);
        _state.SubmittedTemplateId = templateId.ToString();
        _state.SubmittedPanelTitle = "Transfer submitted";
        _state.SubmittedBodyMarkdown = "## Next\n\nWe will contact you.";
        _state.SubmittedPageByTemplate["_default"] = new ApplicationSubmittedPageCopy
        {
            PanelTitle = "Plan submitted",
            BodyMarkdown = "Default body"
        };

        await _service.SaveAsync(_state);

        await _client.Received().UpsertSafeTenantSettingAsync(
            _state.TenantId,
            Arg.Is<UpsertTenantSettingRequest>(r =>
                r.Category == "ApplicationSubmittedPage"
                && Encoding.UTF8.GetString(Convert.FromBase64String(r.SettingsJson))
                    .Contains("Transfer submitted", StringComparison.Ordinal)
                && Encoding.UTF8.GetString(Convert.FromBase64String(r.SettingsJson))
                    .Contains("Plan submitted", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }
}
