using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Application.Tests.Admin;

public class UserManagerAdminServiceTests
{
    private readonly IUsersClient _users = Substitute.For<IUsersClient>();
    private readonly UserManagerAdminService _service;
    private readonly UserManagerWorkState _state = new();

    public UserManagerAdminServiceTests()
    {
        _service = new UserManagerAdminService(_users, NullLogger<UserManagerAdminService>.Instance);
    }

    [Fact]
    public async Task LoadAsync_ShouldSetError_WhenUsersFailToLoad()
    {
        _users.GetTenantUsersAsync(
                Arg.Any<int?>(),
                Arg.Any<int?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("down"));
        _users.GetAccessAuditLogAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(new GetTenantAccessAuditLogDto(Guid.NewGuid(), []));

        await _service.LoadAsync(_state);

        Assert.True(_state.HasError);
        Assert.Equal(UserManagerMessages.LoadFailed, _state.ErrorMessage);
        Assert.Empty(_state.Users);
    }

    [Fact]
    public async Task LoadAsync_ShouldUseApiPaging_WithoutSlicingLocally()
    {
        var page1Items = Enumerable.Range(1, 10)
            .Select(i => new TenantUserDto
            {
                UserId = Guid.NewGuid(),
                Name = $"User {i:00}",
                Email = $"user{i:00}@example.test"
            })
            .ToList();
        var page2Items = Enumerable.Range(11, 2)
            .Select(i => new TenantUserDto
            {
                UserId = Guid.NewGuid(),
                Name = $"User {i:00}",
                Email = $"user{i:00}@example.test"
            })
            .ToList();

        _users.GetTenantUsersAsync(1, 10, null, null, Arg.Any<CancellationToken>())
            .Returns(Paged(page1Items, totalCount: 12, pageNumber: 1, totalPages: 2));
        _users.GetTenantUsersAsync(2, 10, null, null, Arg.Any<CancellationToken>())
            .Returns(Paged(page2Items, totalCount: 12, pageNumber: 2, totalPages: 2));
        _users.GetAccessAuditLogAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(new GetTenantAccessAuditLogDto(Guid.NewGuid(), []));

        _state.CurrentPage = 1;
        await _service.LoadAsync(_state);

        Assert.Equal(12, _state.TotalCount);
        Assert.Equal(2, _state.TotalPages);
        Assert.Equal(10, _state.Users.Count);
        Assert.Equal("User 01", _state.Users[0].Name);

        var page2 = new UserManagerWorkState { CurrentPage = 2 };
        await _service.LoadAsync(page2);

        Assert.Equal(2, page2.Users.Count);
        Assert.Equal("User 11", page2.Users[0].Name);
        Assert.Equal("User 12", page2.Users[1].Name);
        await _users.Received(1).GetTenantUsersAsync(2, 10, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveAsync_ShouldRedirect_WhenApiSucceeds()
    {
        var userId = Guid.NewGuid();

        var result = await _service.RemoveAsync(_state, userId);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal(UserManagerMessages.Removed, result.SuccessMessage);
        await _users.Received(1).RemoveUserFromTenantAsync(userId, Arg.Any<CancellationToken>());
    }

    private static PagedResultOfTenantUserDto Paged(
        IReadOnlyCollection<TenantUserDto> items,
        int totalCount,
        int pageNumber,
        int totalPages) =>
        new()
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = UserManagerWorkState.PageSize,
            TotalPages = totalPages
        };
}
