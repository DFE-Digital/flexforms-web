using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.CoreLibs.Http.Models;
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

        _users.GetCreatedApplicationsByEmailAsync("owner@example.test", Arg.Any<CancellationToken>())
            .Returns(new UserCreatedApplicationsLookupDto
            {
                UserId = userId,
                Name = "Owner",
                Email = "owner@example.test",
                Applications =
                [
                    new CreatedApplicationWithInviteesDto
                    {
                        ApplicationId = applicationId,
                        ApplicationReference = "REF-9",
                        TemplateName = "Transfers",
                        DateCreated = DateTime.UtcNow,
                        Invitees =
                        [
                            new ApplicationInviteeDto
                            {
                                UserId = inviteeId,
                                Name = "Invited",
                                Email = "invitee@example.test"
                            }
                        ]
                    }
                ]
            });

        await _service.LookupByEmailAsync(state);

        Assert.True(state.EmailLookupPerformed);
        Assert.False(state.HasError);
        Assert.Equal(userId, state.LookedUpUserId);
        Assert.Equal("owner@example.test", state.LookedUpUserEmail);
        Assert.Equal(1, state.TotalCount);
        var created = Assert.Single(state.CreatedApplications);
        Assert.Equal("REF-9", created.ApplicationReference);
        var invitee = Assert.Single(created.Invitees);
        Assert.Equal(inviteeId, invitee.UserId);
        Assert.Equal("invitee@example.test", invitee.Email);
        await _applications.DidNotReceiveWithAnyArgs()
            .GetApplicationsForUserAsync(default!, default, default, default);
    }

    [Fact]
    public async Task LookupByEmailAsync_ShouldPaginateApplications()
    {
        var applications = Enumerable.Range(1, 12)
            .Select(i => new CreatedApplicationWithInviteesDto
            {
                ApplicationId = Guid.NewGuid(),
                ApplicationReference = $"REF-{i:00}",
                DateCreated = DateTime.UtcNow.AddDays(-i)
            })
            .ToList();

        _users.GetCreatedApplicationsByEmailAsync("owner@example.test", Arg.Any<CancellationToken>())
            .Returns(new UserCreatedApplicationsLookupDto
            {
                UserId = Guid.NewGuid(),
                Name = "Owner",
                Email = "owner@example.test",
                Applications = applications
            });

        var page1 = new ContributorManagementWorkState { Email = "owner@example.test", CurrentPage = 1 };
        await _service.LookupByEmailAsync(page1);

        Assert.Equal(12, page1.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(10, page1.CreatedApplications.Count);
        Assert.Equal("REF-01", page1.CreatedApplications[0].ApplicationReference);

        var page2 = new ContributorManagementWorkState { Email = "owner@example.test", CurrentPage = 2 };
        await _service.LookupByEmailAsync(page2);

        Assert.Equal(2, page2.CreatedApplications.Count);
        Assert.Equal("REF-11", page2.CreatedApplications[0].ApplicationReference);
        Assert.Equal("REF-12", page2.CreatedApplications[1].ApplicationReference);
    }

    [Fact]
    public async Task LookupByEmailAsync_ShouldSetFriendlyError_WhenEmailIsInvalid()
    {
        var state = new ContributorManagementWorkState { Email = "notanemail" };
        _users.GetCreatedApplicationsByEmailAsync("notanemail", Arg.Any<CancellationToken>())
            .Throws(new ExternalApplicationsException<ExceptionResponse>(
                "Validation failed",
                400,
                "body",
                new Dictionary<string, IEnumerable<string>>(),
                new ExceptionResponse
                {
                    Message = "Validation failed. Please check the following errors:",
                    Details = "Email: 'notanemail' is not a valid email address."
                },
                null));

        await _service.LookupByEmailAsync(state);

        Assert.True(state.HasError);
        Assert.Equal(ContributorManagementMessages.InvalidEmail, state.ErrorMessage);
        Assert.Empty(state.CreatedApplications);
    }

    [Fact]
    public async Task LookupByEmailAsync_ShouldSetError_WhenUserIsUnknown()
    {
        var state = new ContributorManagementWorkState { Email = "missing@example.test" };
        _users.GetCreatedApplicationsByEmailAsync("missing@example.test", Arg.Any<CancellationToken>())
            .Throws(new ExternalApplicationsException("not found", 404, "err", null!, null!));

        await _service.LookupByEmailAsync(state);

        Assert.True(state.HasError);
        Assert.Equal(ContributorManagementMessages.UserNotFound, state.ErrorMessage);
        Assert.Empty(state.CreatedApplications);
    }
}
