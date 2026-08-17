using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Application.Tests.Admin;

public class DuplicateTenantAdminServiceTests
{
    private readonly ITenantAdminClient _client = Substitute.For<ITenantAdminClient>();
    private readonly DuplicateTenantAdminService _service;
    private readonly DuplicateTenantWorkState _state;

    public DuplicateTenantAdminServiceTests()
    {
        _service = new DuplicateTenantAdminService(_client, NullLogger<DuplicateTenantAdminService>.Instance);
        _state = new DuplicateTenantWorkState
        {
            SourceTenantId = Guid.NewGuid(),
            SourceTenantName = "Transfers",
            NewTenantId = Guid.NewGuid(),
            NewTenantName = "Transfers copy",
            ServiceName = "Transfers",
            Hostname = "copy.example.test",
            FrontendOrigin = "https://copy.example.test",
            AuthorizationApiSecretKey = DuplicateTenantAdminService.GenerateSecretKey(),
            InternalServiceAuthSecretKey = DuplicateTenantAdminService.GenerateSecretKey()
        };
    }

    [Fact]
    public void ValidateInput_ShouldReturnError_WhenNewTenantIdIsEmpty()
    {
        _state.NewTenantId = Guid.Empty;

        var errors = _service.ValidateInput(_state);

        Assert.Contains(errors, e => e.Message == DuplicateTenantMessages.TenantIdRequired);
    }

    [Fact]
    public void ValidateInput_ShouldReturnError_WhenNewTenantIdMatchesSource()
    {
        _state.NewTenantId = _state.SourceTenantId;

        var errors = _service.ValidateInput(_state);

        Assert.Contains(errors, e => e.Message == DuplicateTenantMessages.TenantIdMustDiffer);
    }

    [Fact]
    public void ValidateInput_ShouldReturnError_WhenServiceApiKeyIsTooShort()
    {
        _state.InternalServiceAuthServiceApiKeys =
        [
            new DuplicateTenantServiceSecret { Email = "svc@example.test", ApiKey = "short" }
        ];

        var errors = _service.ValidateInput(_state);

        Assert.Contains(errors, e => e.Message == DuplicateTenantMessages.ServiceApiKeyRequired);
    }

    [Fact]
    public async Task LoadInternalServiceAuthServicesAsync_ShouldPopulateApiKeys_WhenInternalServiceAuthExists()
    {
        var tenantId = _state.SourceTenantId;
        _client.GetTenantSettingsAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(new GetTenantSettingsResponse(
                tenantId,
                "Transfers",
                [
                    new TenantSettingDto(
                        Guid.NewGuid(),
                        "InternalServiceAuth",
                        "Api",
                        """{"Services":[{"Email":"svc@example.test"}]}""",
                        false,
                        DateTime.UtcNow)
                ]));

        await _service.LoadInternalServiceAuthServicesAsync(_state);

        var row = Assert.Single(_state.InternalServiceAuthServiceApiKeys);
        Assert.Equal("svc@example.test", row.Email);
        Assert.True(row.ApiKey.Length >= 32);
    }

    [Fact]
    public async Task LoadInternalServiceAuthServicesAsync_ShouldReturnEmpty_WhenCategoryIsMissing()
    {
        _client.GetTenantSettingsAsync(_state.SourceTenantId, Arg.Any<CancellationToken>())
            .Returns(new GetTenantSettingsResponse(_state.SourceTenantId, "Transfers", []));

        await _service.LoadInternalServiceAuthServicesAsync(_state);

        Assert.Empty(_state.InternalServiceAuthServiceApiKeys);
    }

    [Fact]
    public async Task CloneAsync_ShouldSendBase64Payload_WhenInputIsValid()
    {
        var newId = _state.NewTenantId;
        _client.CloneTenantAsync(_state.SourceTenantId, Arg.Any<CloneTenantRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DuplicateTenantResponse(
                _state.SourceTenantId,
                newId,
                "Transfers copy",
                "copy.example.test",
                "https://copy.example.test",
                3,
                "ok"));

        var result = await _service.CloneAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal(
            DuplicateTenantMessages.Created("Transfers copy", newId, 3, "copy.example.test"),
            result.SuccessMessage);
        await _client.Received(1).CloneTenantAsync(
            _state.SourceTenantId,
            Arg.Is<CloneTenantRequest>(r =>
                r.NewTenantId == newId
                && r.NewTenantName == "Transfers copy"
                && !string.IsNullOrWhiteSpace(r.PayloadJson)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CloneAsync_ShouldStay_WhenApiFails()
    {
        _client.CloneTenantAsync(_state.SourceTenantId, Arg.Any<CloneTenantRequest>(), Arg.Any<CancellationToken>())
            .Throws(new ExternalApplicationsException("boom", 500, "err", null!, null!));

        var result = await _service.CloneAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.StayOnPage, result.Kind);
        Assert.Equal(DuplicateTenantMessages.CloneFailedHttp(500), result.ErrorMessage);
        Assert.True(_state.HasError);
    }
}
