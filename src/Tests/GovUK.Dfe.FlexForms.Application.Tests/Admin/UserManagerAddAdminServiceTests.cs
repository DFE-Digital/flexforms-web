using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Application.Tests.Admin;

public class UserManagerAddAdminServiceTests
{
    private readonly IUsersClient _users = Substitute.For<IUsersClient>();
    private readonly ITemplatesClient _templates = Substitute.For<ITemplatesClient>();
    private readonly IRolesClient _roles = Substitute.For<IRolesClient>();
    private readonly UserManagerAddAdminService _service;
    private readonly UserManagerAddWorkState _state;

    public UserManagerAddAdminServiceTests()
    {
        _templates.GetAccessibleTemplatesAsync(Arg.Any<CancellationToken>()).Returns([]);
        _roles.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
        _users.GetTenantUsersAsync(
                Arg.Any<int?>(),
                Arg.Any<int?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(EmptyPage());

        _service = new UserManagerAddAdminService(
            _users,
            _templates,
            _roles,
            NullLogger<UserManagerAddAdminService>.Instance);

        _state = new UserManagerAddWorkState
        {
            Name = "Ada Lovelace",
            Email = "ada@example.com",
            Role = "User",
            SelectedTemplateIds = [Guid.NewGuid()],
            AssignableRoles = ["User", "Caseworker"]
        };
    }

    [Fact]
    public async Task AddAsync_ShouldStay_WhenRoleIsNotAssignable()
    {
        _state.Role = "Admin";

        var result = await _service.AddAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.StayOnPage, result.Kind);
        Assert.Contains(result.Errors, e => e.Message == UserManagerAddMessages.InvalidRole);
        await _users.DidNotReceive().AssignUserRoleAsync(
            Arg.Any<AssignUserRoleRequest>(),
            Arg.Any<bool?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddAsync_ShouldStay_WhenUserRoleHasNoTemplates()
    {
        _state.SelectedTemplateIds = [];

        var result = await _service.AddAsync(_state);

        Assert.Contains(result.Errors, e => e.Message == UserManagerAddMessages.UserRoleRequiresTemplate);
        await _users.DidNotReceive().AssignUserRoleAsync(
            Arg.Any<AssignUserRoleRequest>(),
            Arg.Any<bool?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddAsync_ShouldStay_WhenEmailAlreadyExists()
    {
        _users.GetTenantUsersAsync(
                Arg.Any<int?>(),
                Arg.Any<int?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Page(new TenantUserDto { Email = "ADA@example.com", Name = "Existing" }));

        var result = await _service.AddAsync(_state);

        Assert.Contains(result.Errors, e => e.Message == UserManagerAddMessages.DuplicateEmail);
        await _users.DidNotReceive().AssignUserRoleAsync(
            Arg.Any<AssignUserRoleRequest>(),
            Arg.Any<bool?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddAsync_ShouldAssignRoleAndTemplates_WhenInputIsValid()
    {
        var userId = Guid.NewGuid();
        _users.AssignUserRoleAsync(
                Arg.Any<AssignUserRoleRequest>(),
                Arg.Any<bool?>(),
                Arg.Any<CancellationToken>())
            .Returns(new UserDto { UserId = userId });

        var result = await _service.AddAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal(UserManagerAddMessages.Added("ada@example.com", "User"), result.SuccessMessage);
        await _users.Received(1).AssignUserRoleAsync(
            Arg.Is<AssignUserRoleRequest>(r =>
                r.Name == "Ada Lovelace"
                && r.Email == "ada@example.com"
                && r.Role == "User"),
            true,
            Arg.Any<CancellationToken>());
        await _users.Received(1).UpdateUserTemplateAccessAsync(
            userId,
            Arg.Any<UpdateUserTemplateAccessRequest>(),
            Arg.Any<CancellationToken>());
    }

    private static PagedResultOfTenantUserDto EmptyPage() => Page();

    private static PagedResultOfTenantUserDto Page(params TenantUserDto[] items) =>
        new()
        {
            Items = items,
            TotalCount = items.Length,
            PageNumber = 1,
            PageSize = 1,
            TotalPages = items.Length == 0 ? 0 : 1
        };
}
