using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Application.Dashboard;
using Microsoft.Extensions.Logging.Abstractions;

namespace GovUK.Dfe.FlexForms.Application.Tests.Dashboard;

public class DashboardApplicationStatusCalculatorTests
{
    [Fact]
    public void GetCalculatedStatus_ShouldReturnDeleted_WhenApplicationIsDeletedAndHasFormData()
    {
        var application = new ApplicationDto
        {
            ApplicationId = Guid.NewGuid(),
            ApplicationReference = "REF-DEL",
            Status = ApplicationStatus.Deleted,
            LatestResponse = new ApplicationResponseDetailsDto
            {
                ResponseBody = """{"schoolName":"Example School"}"""
            }
        };

        var result = DashboardApplicationStatusCalculator.GetCalculatedStatus(
            application,
            [],
            NullLogger.Instance);

        Assert.Equal(ApplicationStatus.Deleted, result.Key);
        Assert.Equal("Deleted", result.Value);
    }

    [Fact]
    public void GetCalculatedStatus_ShouldReturnInProgress_WhenApplicationHasFormData()
    {
        var application = new ApplicationDto
        {
            ApplicationId = Guid.NewGuid(),
            ApplicationReference = "REF-IP",
            Status = ApplicationStatus.Created,
            LatestResponse = new ApplicationResponseDetailsDto
            {
                ResponseBody = """{"schoolName":"Example School"}"""
            }
        };

        var result = DashboardApplicationStatusCalculator.GetCalculatedStatus(
            application,
            [],
            NullLogger.Instance);

        Assert.Equal(ApplicationStatus.InProgress, result.Key);
        Assert.Equal("In progress", result.Value);
    }

    [Fact]
    public void GetCalculatedStatus_ShouldReturnSubmitted_WhenApplicationIsSubmitted()
    {
        var application = new ApplicationDto
        {
            ApplicationId = Guid.NewGuid(),
            ApplicationReference = "REF-SUB",
            Status = ApplicationStatus.Submitted,
            LatestResponse = new ApplicationResponseDetailsDto
            {
                ResponseBody = """{"schoolName":"Example School"}"""
            }
        };

        var result = DashboardApplicationStatusCalculator.GetCalculatedStatus(
            application,
            [],
            NullLogger.Instance);

        Assert.Equal(ApplicationStatus.Submitted, result.Key);
        Assert.Equal("Submitted", result.Value);
    }
}
