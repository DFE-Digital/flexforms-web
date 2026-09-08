using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
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

    [Fact]
    public async Task LoadAsync_ShouldPopulateSettings_WhenApiSucceeds()
    {
        _client.GetTenantSettingsAsync(_state.TenantId, Arg.Any<CancellationToken>())
            .Returns(new GetTenantSettingsResponse(
                _state.TenantId,
                "Transfers",
                [new TenantSettingDto(Guid.NewGuid(), "Layout", "Web", "{}", false, DateTime.UtcNow)]));

        await _service.LoadAsync(_state);

        Assert.Equal("Transfers", _state.TenantName);
        Assert.Single(_state.Settings);
        Assert.False(_state.HasError);
    }

    [Fact]
    public async Task LoadAsync_ShouldSetError_WhenSettingsApiFails()
    {
        _client.GetTenantSettingsAsync(_state.TenantId, Arg.Any<CancellationToken>())
            .Throws(new ExternalApplicationsException("boom", 500, "err", null!, null!));

        await _service.LoadAsync(_state);

        Assert.True(_state.HasError);
        Assert.Equal(TenantSettingsMessages.LoadFailed + " (HTTP 500)", _state.ErrorMessage);
        Assert.Empty(_state.Settings);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpsertSecretSetting_WhenInputIsValid()
    {
        var result = await _service.UpdateAsync(_state, "Secrets", "Api", """{"key":"value"}""", isSecret: true);

        Assert.Equal(TenantSettingsMessages.Updated("Secrets", "Api"), result.SuccessMessage);
        await _client.Received(1).UpsertTenantSettingAsync(
            _state.TenantId,
            Arg.Is<UpsertTenantSettingRequest>(r => r.IsSecret && r.Category == "Secrets"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_ShouldRedirectWithError_WhenApiFails()
    {
        _client.UpsertTenantSettingAsync(
                Arg.Any<Guid>(),
                Arg.Any<UpsertTenantSettingRequest>(),
                Arg.Any<CancellationToken>())
            .Throws(new ExternalApplicationsException("boom", 500, "err", null!, null!));

        var result = await _service.UpdateAsync(_state, "Layout", "Web", "{}", false);

        Assert.Equal(TenantSettingsMessages.UpdateFailed + " (HTTP 500)", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_ShouldCallApi_WhenInputIsValid()
    {
        var preview = new ValidateTenantSettingResponse(true, [], null, null, "{}", false);
        _client.ValidateTenantSettingAsync(_state.TenantId, Arg.Any<ValidateTenantSettingRequest>(), Arg.Any<CancellationToken>())
            .Returns(preview);
        _client.GetTenantSettingsAsync(_state.TenantId, Arg.Any<CancellationToken>())
            .Returns(new GetTenantSettingsResponse(_state.TenantId, "Transfers", []));

        var result = await _service.ValidateAsync(_state, "Layout", "Web", "{}", false);

        Assert.Equal(AdminPageOutcomeKind.StayOnPage, result.Kind);
        Assert.Equal(preview, _state.ValidationPreview);
        Assert.False(_state.HasError);
    }

    [Fact]
    public async Task ExportAsync_ShouldReturnFile_WhenApiSucceeds()
    {
        _client.ExportConfigurationAsync(_state.TenantId, Arg.Any<CancellationToken>())
            .Returns(new ExportTenantConfigurationDto(_state.TenantId, "Transfers", DateTimeOffset.UtcNow, []));

        var result = await _service.ExportAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.FileDownload, result.Kind);
        Assert.Equal("application/json", result.FileContentType);
        Assert.NotNull(result.FileBytes);
    }

    [Fact]
    public async Task ExportAsync_ShouldRedirect_WhenApiFails()
    {
        _client.ExportConfigurationAsync(_state.TenantId, Arg.Any<CancellationToken>())
            .Throws(new ExternalApplicationsException("boom", 502, "err", null!, null!));

        var result = await _service.ExportAsync(_state);

        Assert.Equal(TenantSettingsMessages.ExportFailed + " (HTTP 502)", result.ErrorMessage);
    }

    [Fact]
    public async Task ImportAsync_ShouldImportAndRefresh_WhenJsonIsValid()
    {
        var json = """
            {
              "settings": [
                { "category": "Layout", "target": "Web", "settingsJson": "{}", "isSecret": false }
              ]
            }
            """;
        _client.ImportConfigurationAsync(_state.TenantId, Arg.Any<ImportTenantConfigurationDto>(), Arg.Any<CancellationToken>())
            .Returns(new ImportTenantConfigurationResultDto(1, 0, []));

        var result = await _service.ImportAsync(_state, json);

        Assert.Equal(TenantSettingsMessages.Imported(1, 0), result.SuccessMessage);
        Assert.True(result.RefreshLocalCaches);
    }

    [Fact]
    public async Task RefreshAsync_ShouldRedirectWithSuccess_WhenApiSucceeds()
    {
        var result = await _service.RefreshAsync(_state);

        Assert.Equal(TenantSettingsMessages.RefreshSuccess, result.SuccessMessage);
        await _client.Received(1).RefreshTenantConfigurationAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_ShouldRedirectWithError_WhenApiFails()
    {
        _client.DeleteTenantSettingAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new ExternalApplicationsException("boom", 404, "err", null!, null!));

        var result = await _service.DeleteAsync(_state, "Layout", "Web");

        Assert.Equal(TenantSettingsMessages.DeleteFailed + " (HTTP 404)", result.ErrorMessage);
    }
}
