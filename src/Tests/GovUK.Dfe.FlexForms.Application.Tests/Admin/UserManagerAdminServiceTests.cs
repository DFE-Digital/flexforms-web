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
        _users.GetTenantUsersAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("down"));
        _users.GetAccessAuditLogAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(new GetTenantAccessAuditLogDto(Guid.NewGuid(), []));

        await _service.LoadAsync(_state);

        Assert.True(_state.HasError);
        Assert.Equal(UserManagerMessages.LoadFailed, _state.ErrorMessage);
        Assert.Empty(_state.Users);
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
}
