using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Application.Tests.Admin;

public class TenantSettingsAdminServiceTests
{
    private readonly ITenantAdminClient _client = Substitute.For<ITenantAdminClient>();
    private readonly TenantSettingsAdminService _service;
    private readonly TenantSettingsWorkState _state = new()
    {
        TenantId = Guid.NewGuid(),
        TenantName = "Transfers"
    };

    public TenantSettingsAdminServiceTests()
    {
        _service = new TenantSettingsAdminService(_client, NullLogger<TenantSettingsAdminService>.Instance);
    }

    [Fact]
    public async Task AddAsync_ShouldRedirect_WhenCategoryIsMissing()
    {
        var result = await _service.AddAsync(_state, "  ", "Shared", "{}", false);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal(TenantSettingsMessages.CategoryRequired, result.ErrorMessage);
        await _client.DidNotReceive().UpsertTenantSettingAsync(
            Arg.Any<Guid>(),
            Arg.Any<UpsertTenantSettingRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddAsync_ShouldRedirect_WhenCategoryExceedsFiftyCharacters()
    {
        var result = await _service.AddAsync(_state, new string('a', 51), "Shared", "{}", false);

        Assert.Equal(TenantSettingsMessages.CategoryTooLong, result.ErrorMessage);
    }

    [Fact]
    public async Task AddAsync_ShouldRedirect_WhenTargetIsInvalid()
    {
        var result = await _service.AddAsync(_state, "Layout", "Desktop", "{}", false);

        Assert.Equal(TenantSettingsMessages.InvalidTarget, result.ErrorMessage);
    }

    [Fact]
    public async Task AddAsync_ShouldUpsertAndRefresh_WhenInputIsValid()
    {
        var result = await _service.AddAsync(_state, "Layout", "Web", """{"x":1}""", true);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal(TenantSettingsMessages.Added("Layout", "Web"), result.SuccessMessage);
        Assert.True(result.RefreshLocalCaches);
        await _client.Received(1).UpsertTenantSettingAsync(
            _state.TenantId,
            Arg.Is<UpsertTenantSettingRequest>(r =>
                r.Category == "Layout"
                && r.Target == "Web"
                && r.IsSecret
                && r.SettingsJson == AdminSettingsEncoding.ToBase64("""{"x":1}""")),
            Arg.Any<CancellationToken>());
        await _client.Received(1).RefreshTenantConfigurationAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_ShouldRedirect_WhenJsonIsMissing()
    {
        var result = await _service.UpdateAsync(_state, "Layout", "Web", "  ", false);

        Assert.Equal(TenantSettingsMessages.CategoryAndJsonRequired, result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_ShouldStay_WhenCategoryOrJsonMissing()
    {
        var result = await _service.ValidateAsync(_state, "", "Web", "", false);

        Assert.Equal(AdminPageOutcomeKind.StayOnPage, result.Kind);
        Assert.Equal(TenantSettingsMessages.ValidateRequired, result.ErrorMessage);
        Assert.True(_state.HasError);
        await _client.DidNotReceive().ValidateTenantSettingAsync(
            Arg.Any<Guid>(),
            Arg.Any<ValidateTenantSettingRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportAsync_ShouldRedirect_WhenJsonIsInvalid()
    {
        var result = await _service.ImportAsync(_state, "not-json");

        Assert.Equal(TenantSettingsMessages.ImportInvalidJson, result.ErrorMessage);
    }

    [Fact]
    public async Task ImportAsync_ShouldRedirect_WhenSettingsAreEmpty()
    {
        var result = await _service.ImportAsync(_state, """{"settings":[]}""");

        Assert.Equal(TenantSettingsMessages.ImportEmpty, result.ErrorMessage);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRefreshCaches_WhenApiSucceeds()
    {
        var result = await _service.DeleteAsync(_state, "Layout", "Web");

        Assert.Equal(TenantSettingsMessages.Deleted("Layout", "Web"), result.SuccessMessage);
        Assert.True(result.RefreshLocalCaches);
        await _client.Received(1).DeleteTenantSettingAsync(_state.TenantId, "Layout", "Web", Arg.Any<CancellationToken>());
    }
}
