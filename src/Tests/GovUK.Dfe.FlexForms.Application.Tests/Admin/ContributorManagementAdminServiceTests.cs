using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Application.Tests.Admin;

public class ContributorManagementAdminServiceTests
{
    private readonly IApplicationsClient _applications = Substitute.For<IApplicationsClient>();
    private readonly IUsersClient _users = Substitute.For<IUsersClient>();
    private readonly ContributorManagementAdminService _service;

    public ContributorManagementAdminServiceTests()
    {
        _service = new ContributorManagementAdminService(
            _applications,
            _users,
            NullLogger<ContributorManagementAdminService>.Instance);
    }

    [Fact]
    public async Task LookupAsync_ShouldPopulateContributors_WhenApplicationExists()
    {
        var applicationId = Guid.NewGuid();
        var state = new ContributorManagementWorkState { ReferenceNumber = "REF-1" };
        _applications.GetApplicationByReferenceAsync("REF-1", Arg.Any<CancellationToken>())
            .Returns(new ApplicationDto
            {
                ApplicationId = applicationId,
                ApplicationReference = "REF-1",
                TemplateName = "Transfers"
            });
        _applications.GetContributorsAsync(applicationId, false, Arg.Any<CancellationToken>())
            .Returns(
            [
                new UserDto { Name = "Zoe", Email = "z@example.test" },
                new UserDto { Name = "Ann", Email = "a@example.test" }
            ]);

        await _service.LookupAsync(state);

        Assert.True(state.LookupPerformed);
        Assert.Equal(applicationId, state.ApplicationId);
        Assert.Equal("Transfers", state.TemplateName);
        Assert.Equal(new[] { "Ann", "Zoe" }, state.Contributors.Select(c => c.Name).ToArray());
    }

    [Fact]
    public async Task LookupAsync_ShouldSetError_WhenApiFails()
    {
        var state = new ContributorManagementWorkState { ReferenceNumber = "MISSING" };
        _applications.GetApplicationByReferenceAsync("MISSING", Arg.Any<CancellationToken>())
            .Throws(new ExternalApplicationsException("boom", 404, "err", null!, null!));

        await _service.LookupAsync(state);

        Assert.True(state.HasError);
        Assert.Equal(ContributorManagementMessages.LookupFailed + " (HTTP 404)", state.ErrorMessage);
        Assert.Empty(state.Contributors);
    }

    [Fact]
    public async Task LookupByEmailAsync_ShouldShowCreatedApplicationsAndInvitees()
    {
        var userId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var inviteeId = Guid.NewGuid();
        var state = new ContributorManagementWorkState { Email = "owner@example.test" };

        _users.GetTenantUsersAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new TenantUserDto
            {
                UserId = userId,
                Name = "Owner",
                Email = "owner@example.test"
            }
        ]);
        _applications.GetApplicationsForUserAsync("owner@example.test", false, null, Arg.Any<CancellationToken>())
            .Returns(new PagedResultOfApplicationDto
            {
                Items =
                [
                    new ApplicationDto { ApplicationId = applicationId, ApplicationReference = "REF-9" }
                ]
            });
        _applications.GetApplicationByReferenceAsync("REF-9", Arg.Any<CancellationToken>())
            .Returns(new ApplicationDto
            {
                ApplicationId = applicationId,
                ApplicationReference = "REF-9",
                TemplateName = "Transfers",
                CreatedBy = new UserDto { UserId = userId, Email = "owner@example.test", Name = "Owner" }
            });
        _applications.GetContributorsAsync(applicationId, false, Arg.Any<CancellationToken>())
            .Returns(
            [
                new UserDto { UserId = inviteeId, Name = "Invited", Email = "invitee@example.test" }
            ]);

        await _service.LookupByEmailAsync(state);

        Assert.True(state.EmailLookupPerformed);
        Assert.False(state.HasError);
        Assert.Equal(userId, state.LookedUpUserId);
        Assert.Equal("owner@example.test", state.LookedUpUserEmail);
        var created = Assert.Single(state.CreatedApplications);
        Assert.Equal("REF-9", created.ApplicationReference);
        var invitee = Assert.Single(created.Invitees);
        Assert.Equal(inviteeId, invitee.UserId);
        Assert.Equal("invitee@example.test", invitee.Email);
    }

    [Fact]
    public async Task LookupByEmailAsync_ShouldIgnoreApplicationsTheUserDidNotCreate()
    {
        var userId = Guid.NewGuid();
        var state = new ContributorManagementWorkState { Email = "owner@example.test" };

        _users.GetTenantUsersAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new TenantUserDto { UserId = userId, Name = "Owner", Email = "owner@example.test" }
        ]);
        _applications.GetApplicationsForUserAsync("owner@example.test", false, null, Arg.Any<CancellationToken>())
            .Returns(new PagedResultOfApplicationDto
            {
                Items =
                [
                    new ApplicationDto { ApplicationReference = "REF-OTHER" }
                ]
            });
        _applications.GetApplicationByReferenceAsync("REF-OTHER", Arg.Any<CancellationToken>())
            .Returns(new ApplicationDto
            {
                ApplicationReference = "REF-OTHER",
                CreatedBy = new UserDto { UserId = Guid.NewGuid(), Email = "someone-else@example.test" }
            });

        await _service.LookupByEmailAsync(state);

        Assert.Empty(state.CreatedApplications);
        Assert.Equal(userId, state.LookedUpUserId);
    }

    [Fact]
    public async Task LookupByEmailAsync_ShouldSetError_WhenUserIsUnknown()
    {
        var state = new ContributorManagementWorkState { Email = "missing@example.test" };
        _users.GetTenantUsersAsync(Arg.Any<CancellationToken>()).Returns([]);

        await _service.LookupByEmailAsync(state);

        Assert.True(state.HasError);
        Assert.Equal(ContributorManagementMessages.UserNotFound, state.ErrorMessage);
        await _applications.DidNotReceiveWithAnyArgs()
            .GetApplicationsForUserAsync(default!, default, default, default);
    }

    [Fact]
    public async Task LookupByEmailAsync_ShouldNotLookUpApplications_WhenUserBelongsToAnotherTenant()
    {
        var state = new ContributorManagementWorkState { Email = "other-tenant@example.test" };
        _users.GetTenantUsersAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new TenantUserDto
            {
                UserId = Guid.NewGuid(),
                Name = "Someone else",
                Email = "this-tenant@example.test"
            }
        ]);

        await _service.LookupByEmailAsync(state);

        Assert.True(state.HasError);
        Assert.Equal(ContributorManagementMessages.UserNotFound, state.ErrorMessage);
        Assert.Empty(state.CreatedApplications);
        await _applications.DidNotReceiveWithAnyArgs()
            .GetApplicationsForUserAsync(default!, default, default, default);
    }
}
