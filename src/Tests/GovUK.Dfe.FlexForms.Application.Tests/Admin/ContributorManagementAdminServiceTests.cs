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
    private readonly IApplicationsClient _client = Substitute.For<IApplicationsClient>();
    private readonly ContributorManagementAdminService _service;

    public ContributorManagementAdminServiceTests()
    {
        _service = new ContributorManagementAdminService(
            _client,
            NullLogger<ContributorManagementAdminService>.Instance);
    }

    [Fact]
    public async Task LookupAsync_ShouldPopulateContributors_WhenApplicationExists()
    {
        var applicationId = Guid.NewGuid();
        var state = new ContributorManagementWorkState { ReferenceNumber = "REF-1" };
        _client.GetApplicationByReferenceAsync("REF-1", Arg.Any<CancellationToken>())
            .Returns(new ApplicationDto
            {
                ApplicationId = applicationId,
                ApplicationReference = "REF-1",
                TemplateName = "Transfers"
            });
        _client.GetContributorsAsync(applicationId, false, Arg.Any<CancellationToken>())
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
        _client.GetApplicationByReferenceAsync("MISSING", Arg.Any<CancellationToken>())
            .Throws(new ExternalApplicationsException("boom", 404, "err", null!, null!));

        await _service.LookupAsync(state);

        Assert.True(state.HasError);
        Assert.Equal(ContributorManagementMessages.LookupFailed + " (HTTP 404)", state.ErrorMessage);
        Assert.Empty(state.Contributors);
    }
}
