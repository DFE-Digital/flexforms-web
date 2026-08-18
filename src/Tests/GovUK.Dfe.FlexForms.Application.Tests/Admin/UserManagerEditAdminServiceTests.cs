using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Application.Tests.Admin;

public class UserManagerEditAdminServiceTests
{
    private readonly IUsersClient _users = Substitute.For<IUsersClient>();
    private readonly ITemplatesClient _templates = Substitute.For<ITemplatesClient>();
    private readonly IRolesClient _roles = Substitute.For<IRolesClient>();
    private readonly UserManagerEditAdminService _service;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly UserManagerEditWorkState _state;

    public UserManagerEditAdminServiceTests()
    {
        _templates.GetAccessibleTemplatesAsync(Arg.Any<CancellationToken>()).Returns([]);
        _roles.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new TenantRoleDto { RoleId = Guid.NewGuid(), Name = "User", IsSystem = true },
            new TenantRoleDto { RoleId = Guid.NewGuid(), Name = "Caseworker", IsSystem = false }
        ]);

        _service = new UserManagerEditAdminService(
            _users,
            _templates,
            _roles,
            NullLogger<UserManagerEditAdminService>.Instance);

        _state = new UserManagerEditWorkState
        {
            UserId = _userId,
            UserName = "Ada Lovelace",
            UserEmail = "ada@example.com",
            Role = "Caseworker",
            AssignableRoles = ["User", "Caseworker"],
            SelectedTemplateIds = [Guid.NewGuid()]
        };
    }

    [Fact]
    public async Task LoadAsync_ShouldRedirect_WhenUserIsMissing()
    {
        _users.GetTenantUsersAsync(Arg.Any<CancellationToken>()).Returns([]);

        var result = await _service.LoadAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal(UserManagerEditMessages.UserNotFound, result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateAsync_ShouldStay_WhenRoleIsNotAssignable()
    {
        _state.Role = "Admin";

        var result = await _service.UpdateAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.StayOnPage, result.Kind);
        Assert.Contains(result.Errors, e => e.Message == UserManagerEditMessages.InvalidRole);
        await _users.DidNotReceive().AssignUserRoleAsync(
            Arg.Any<AssignUserRoleRequest>(),
            Arg.Any<bool?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_ShouldAssignRoleAndTemplates_WhenRoleChanged()
    {
        _users.GetTenantUsersAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new TenantUserDto
            {
                UserId = _userId,
                Name = "Ada Lovelace",
                Email = "ada@example.com",
                Role = "User"
            }
        ]);

        var result = await _service.UpdateAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal(UserManagerEditMessages.Updated, result.SuccessMessage);
        await _users.Received(1).AssignUserRoleAsync(
            Arg.Is<AssignUserRoleRequest>(r =>
                r.Name == "Ada Lovelace"
                && r.Email == "ada@example.com"
                && r.Role == "Caseworker"),
            false,
            Arg.Any<CancellationToken>());
        await _users.Received(1).UpdateUserTemplateAccessAsync(
            _userId,
            Arg.Any<UpdateUserTemplateAccessRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateTemplatesOnly_WhenRoleIsUnchanged()
    {
        _users.GetTenantUsersAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new TenantUserDto
            {
                UserId = _userId,
                Name = "Ada Lovelace",
                Email = "ada@example.com",
                Role = "Caseworker"
            }
        ]);

        var result = await _service.UpdateAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal(UserManagerEditMessages.Updated, result.SuccessMessage);
        await _users.DidNotReceive().AssignUserRoleAsync(
            Arg.Any<AssignUserRoleRequest>(),
            Arg.Any<bool?>(),
            Arg.Any<CancellationToken>());
        await _users.Received(1).UpdateUserTemplateAccessAsync(
            _userId,
            Arg.Any<UpdateUserTemplateAccessRequest>(),
            Arg.Any<CancellationToken>());
    }
}
