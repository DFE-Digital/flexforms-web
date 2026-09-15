using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
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
        _users.GetTenantUsersAsync(
                Arg.Any<int?>(),
                Arg.Any<int?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new PagedResultOfTenantUserDto
            {
                Items = [new TenantUserDto { UserId = _userId, Name = "Ada", Email = "ada@example.com" }],
                TotalCount = 1,
                PageNumber = 1,
                PageSize = 1,
                TotalPages = 1
            });
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

    [Fact]
    public async Task LoadAsync_ShouldStay_WhenUserAndPermissionsLoad()
    {
        var result = await _service.LoadAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.StayOnPage, result.Kind);
        Assert.Equal("Ada", _state.UserName);
        Assert.Equal("ada@example.com", _state.UserEmail);
    }

    [Fact]
    public async Task LoadAsync_ShouldRedirect_WhenUserIsMissing()
    {
        _users.GetTenantUsersAsync(
                Arg.Any<int?>(),
                Arg.Any<int?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new PagedResultOfTenantUserDto
            {
                Items = [],
                TotalCount = 0,
                PageNumber = 1,
                PageSize = 1,
                TotalPages = 0
            });

        var result = await _service.LoadAsync(_state);

        Assert.Equal(UserManagerPermissionsMessages.UserNotFound, result.ErrorMessage);
    }

    [Fact]
    public async Task LoadAsync_ShouldRedirect_WhenPermissionsLoadFails()
    {
        _users.GetUserPermissionsAsync(_userId, Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("permissions unavailable"));

        var result = await _service.LoadAsync(_state);

        Assert.Contains(UserManagerPermissionsMessages.LoadPermissionsFailed, result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_ShouldRedirect_WhenUserLookupFails()
    {
        _users.GetTenantUsersAsync(
                Arg.Any<int?>(),
                Arg.Any<int?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("user lookup failed"));

        var result = await _service.LoadAsync(_state);

        Assert.Contains(UserManagerPermissionsMessages.LoadUserFailed, result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddGrantAsync_ShouldStay_WhenGrantAlreadyExists()
    {
        var applicationId = Guid.NewGuid();
        _state.NewResourceKey = applicationId.ToString();
        _state.SelectedGrants =
        [
            AdminPermissionGrants.EncodeGrantKey(ResourceType.Application, applicationId.ToString(), AccessType.Read)
        ];

        var result = await _service.AddGrantAsync(_state);

        Assert.Contains(
            result.Errors,
            e => e.Message == UserManagerPermissionsMessages.DuplicateGrant(
                ResourceType.Application.ToString(),
                applicationId.ToString(),
                AccessType.Read.ToString()));
        await _users.DidNotReceive().SetUserPermissionsAsync(
            Arg.Any<Guid>(),
            Arg.Any<SetUserPermissionsRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveGrantAsync_ShouldSaveRemainingGrants()
    {
        var applicationId = Guid.NewGuid();
        var grantKey = AdminPermissionGrants.EncodeGrantKey(
            ResourceType.Application,
            applicationId.ToString(),
            AccessType.Read);
        _state.SelectedGrants = [grantKey];

        var result = await _service.RemoveGrantAsync(_state, grantKey);

        Assert.Equal(AdminPageOutcomeKind.StayOnPage, result.Kind);
        Assert.Empty(_state.SelectedGrants);
        await _users.Received(1).SetUserPermissionsAsync(
            _userId,
            Arg.Is<SetUserPermissionsRequest>(r => r.Permissions.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddGrantAsync_ShouldStay_WhenSaveFails()
    {
        var applicationId = Guid.NewGuid();
        _state.NewResourceKey = applicationId.ToString();
        _users.SetUserPermissionsAsync(
                _userId,
                Arg.Any<SetUserPermissionsRequest>(),
                Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("save failed"));

        var result = await _service.AddGrantAsync(_state);

        Assert.Contains(result.Errors, e => e.Message.Contains(UserManagerPermissionsMessages.SaveFailed));
    }
}
