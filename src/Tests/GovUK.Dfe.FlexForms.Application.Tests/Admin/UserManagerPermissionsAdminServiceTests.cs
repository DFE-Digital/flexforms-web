using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Application.Tests.Admin;

public class UserManagerPermissionsAdminServiceTests
{
    private readonly IUsersClient _users = Substitute.For<IUsersClient>();
    private readonly UserManagerPermissionsAdminService _service;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly UserManagerPermissionsWorkState _state;

    public UserManagerPermissionsAdminServiceTests()
    {
        _users.GetTenantUsersAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new TenantUserDto { UserId = _userId, Name = "Ada", Email = "ada@example.com" }
        ]);
        _users.GetUserPermissionsAsync(_userId, Arg.Any<CancellationToken>()).Returns([]);

        _service = new UserManagerPermissionsAdminService(
            _users,
            NullLogger<UserManagerPermissionsAdminService>.Instance);

        _state = new UserManagerPermissionsWorkState
        {
            UserId = _userId,
            NewResourceType = ResourceType.Application,
            NewAccessType = AccessType.Read
        };
    }

    [Fact]
    public async Task AddGrantAsync_ShouldStay_WhenResourceKeyIsMissing()
    {
        _state.NewResourceKey = "  ";

        var result = await _service.AddGrantAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.StayOnPage, result.Kind);
        Assert.Contains(result.Errors, e => e.Message == UserManagerPermissionsMessages.ResourceKeyRequired);
        await _users.DidNotReceive().SetUserPermissionsAsync(
            Arg.Any<Guid>(),
            Arg.Any<SetUserPermissionsRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddGrantAsync_ShouldStay_WhenManageIsGrantedToUser()
    {
        _state.NewResourceType = ResourceType.Template;
        _state.NewResourceKey = AdminPermissionGrants.AnyResourceKey;
        _state.NewAccessType = AccessType.Manage;

        var result = await _service.AddGrantAsync(_state);

        Assert.Contains(result.Errors, e => e.Message.Contains("cannot be granted to an individual user"));
        await _users.DidNotReceive().SetUserPermissionsAsync(
            Arg.Any<Guid>(),
            Arg.Any<SetUserPermissionsRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddGrantAsync_ShouldSave_WhenGrantIsValid()
    {
        var applicationId = Guid.NewGuid();
        _state.NewResourceKey = applicationId.ToString();

        var result = await _service.AddGrantAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.StayOnPage, result.Kind);
        Assert.Empty(result.Errors);
        Assert.Equal(string.Empty, _state.NewResourceKey);
        await _users.Received(1).SetUserPermissionsAsync(
            _userId,
            Arg.Is<SetUserPermissionsRequest>(r =>
                r.Permissions.Count == 1
                && r.Permissions.First().ResourceType == ResourceType.Application
                && r.Permissions.First().ResourceKey == applicationId.ToString()
                && r.Permissions.First().AccessType == AccessType.Read),
            Arg.Any<CancellationToken>());
    }
}
