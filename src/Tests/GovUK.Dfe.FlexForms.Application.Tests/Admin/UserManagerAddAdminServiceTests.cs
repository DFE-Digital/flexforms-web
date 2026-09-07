using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
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

    [Fact]
    public async Task LoadAsync_ShouldPopulateTemplatesAndRoles_WhenLookupsSucceed()
    {
        var templateId = Guid.NewGuid();
        _templates.GetAccessibleTemplatesAsync(Arg.Any<CancellationToken>()).Returns([
            new TemplateDto { TemplateId = templateId, Name = "Transfers", CreatedOn = DateTime.UtcNow }
        ]);
        _roles.ListAsync(Arg.Any<CancellationToken>()).Returns([
            new TenantRoleDto { RoleId = Guid.NewGuid(), Name = "User", IsSystem = true },
            new TenantRoleDto { RoleId = Guid.NewGuid(), Name = "Caseworker", IsSystem = false }
        ]);

        await _service.LoadAsync(_state);

        Assert.Single(_state.AvailableTemplates);
        Assert.Equal("Transfers", _state.AvailableTemplates[0].Name);
        Assert.Contains("User", _state.AssignableRoles);
        Assert.Contains("Caseworker", _state.AssignableRoles);
        Assert.Empty(_state.Errors);
    }

    [Fact]
    public async Task LoadAsync_ShouldRecordTemplateError_WhenTemplatesApiFails()
    {
        _templates.GetAccessibleTemplatesAsync(Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("templates unavailable"));

        await _service.LoadAsync(_state);

        Assert.Empty(_state.AvailableTemplates);
        Assert.Contains(_state.Errors, e => e.Message.Contains(UserManagerAddMessages.LoadTemplatesFailed));
    }

    [Fact]
    public async Task LoadAsync_ShouldRecordRoleError_WhenRolesApiFails()
    {
        _roles.ListAsync(Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("roles unavailable"));

        await _service.LoadAsync(_state);

        Assert.NotEmpty(_state.AssignableRoles);
        Assert.Contains(_state.Errors, e => e.Message.Contains(UserManagerAddMessages.LoadRolesFailed));
    }

    [Fact]
    public async Task AddAsync_ShouldRedirect_WhenCaseworkerHasNoTemplates()
    {
        _state.Role = "Caseworker";
        _state.SelectedTemplateIds = [];
        var userId = Guid.NewGuid();
        _users.AssignUserRoleAsync(
                Arg.Any<AssignUserRoleRequest>(),
                Arg.Any<bool?>(),
                Arg.Any<CancellationToken>())
            .Returns(new UserDto { UserId = userId });

        var result = await _service.AddAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        await _users.DidNotReceive().UpdateUserTemplateAccessAsync(
            Arg.Any<Guid>(),
            Arg.Any<UpdateUserTemplateAccessRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddAsync_ShouldStay_WhenAssignUserRoleFails()
    {
        _users.AssignUserRoleAsync(
                Arg.Any<AssignUserRoleRequest>(),
                Arg.Any<bool?>(),
                Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("assign failed"));

        var result = await _service.AddAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.StayOnPage, result.Kind);
        Assert.Contains(result.Errors, e => e.Message.Contains(UserManagerAddMessages.AddFailed));
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
