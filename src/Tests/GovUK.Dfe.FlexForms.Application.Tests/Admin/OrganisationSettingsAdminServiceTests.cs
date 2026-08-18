using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Application.Tests.Admin;

public class OrganisationSettingsAdminServiceTests
{
    private readonly ITenantAdminClient _client = Substitute.For<ITenantAdminClient>();
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
        _service = new OrganisationSettingsAdminService(_client, NullLogger<OrganisationSettingsAdminService>.Instance);
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
                        DateTime.UtcNow)
                ]));

        await _service.LoadAsync(_state);

        Assert.Equal("Loaded tenant", _state.TenantName);
        Assert.Equal("reform", _state.TerminologySingular);
        Assert.Equal("reforms", _state.TerminologyPlural);
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
        await _client.Received(3).UpsertSafeTenantSettingAsync(
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
}
