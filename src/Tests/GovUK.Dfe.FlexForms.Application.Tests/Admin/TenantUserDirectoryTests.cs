using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Application.Tests.Admin;

public class TenantUserDirectoryTests
{
    [Fact]
    public async Task GetByIdAsync_ShouldReturnMatchingUser()
    {
        var users = Substitute.For<IUsersClient>();
        var userId = Guid.NewGuid();
        users.GetTenantUsersAsync(1, 1, userId, null, Arg.Any<CancellationToken>())
            .Returns(new PagedResultOfTenantUserDto
            {
                Items = [new TenantUserDto { UserId = userId, Email = "ada@example.com" }]
            });

        var found = await TenantUserDirectory.GetByIdAsync(users, userId, CancellationToken.None);

        Assert.Equal(userId, found!.UserId);
    }

    [Fact]
    public async Task EmailExistsAsync_ShouldBeCaseInsensitive()
    {
        var users = Substitute.For<IUsersClient>();
        users.GetTenantUsersAsync(1, 1, null, "ada@example.com", Arg.Any<CancellationToken>())
            .Returns(new PagedResultOfTenantUserDto
            {
                Items = [new TenantUserDto { UserId = Guid.NewGuid(), Email = "ADA@example.com" }]
            });

        Assert.True(await TenantUserDirectory.EmailExistsAsync(users, " ada@example.com ", CancellationToken.None));
    }
}
