using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Application.Tests.Admin;

public class RoleManagerPermissionsAdminServiceTests
{
    private readonly IRolesClient _roles = Substitute.For<IRolesClient>();
    private readonly RoleManagerPermissionsAdminService _service;
    private readonly Guid _roleId = Guid.NewGuid();
    private readonly RoleManagerPermissionsWorkState _state;

    public RoleManagerPermissionsAdminServiceTests()
    {
        _roles.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new TenantRoleDto { RoleId = _roleId, Name = "Caseworker", IsSystem = false }
        ]);
        _roles.GetPermissionsAsync(_roleId, Arg.Any<CancellationToken>()).Returns([]);

        _service = new RoleManagerPermissionsAdminService(
            _roles,
            NullLogger<RoleManagerPermissionsAdminService>.Instance);

        _state = new RoleManagerPermissionsWorkState
        {
            RoleId = _roleId,
            NewResourceType = ResourceType.Application,
            NewAccessType = AccessType.Read
        };
    }

    [Fact]
    public async Task AddGrantAsync_ShouldStay_WhenResourceKeyIsMissing()
    {
        _state.NewResourceKey = "";

        var result = await _service.AddGrantAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.StayOnPage, result.Kind);
        Assert.Contains(result.Errors, e => e.Message == RoleManagerPermissionsMessages.ResourceKeyRequired);
        await _roles.DidNotReceive().SetPermissionsAsync(
            Arg.Any<Guid>(),
            Arg.Any<SetRolePermissionsRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadAsync_ShouldRedirect_WhenRoleIsSystem()
    {
        _roles.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new TenantRoleDto { RoleId = _roleId, Name = "Admin", IsSystem = true }
        ]);

        var result = await _service.LoadAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal(RoleManagerPermissionsMessages.SystemRoleCannotChangeCreateCustom, result.ErrorMessage);
    }

    [Fact]
    public async Task AddGrantAsync_ShouldSave_WhenGrantIsValid()
    {
        var applicationId = Guid.NewGuid();
        _state.NewResourceKey = applicationId.ToString();

        var result = await _service.AddGrantAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.StayOnPage, result.Kind);
        Assert.Empty(result.Errors);
        await _roles.Received(1).SetPermissionsAsync(
            _roleId,
            Arg.Is<SetRolePermissionsRequest>(r =>
                r.Permissions.Count == 1
                && r.Permissions.First().ResourceKey == applicationId.ToString()),
            Arg.Any<CancellationToken>());
    }
}
