using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using GovUK.Dfe.CoreLibs.Caching.Interfaces;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Services;

public class ApplicationStatusServiceTests
{
    private readonly ITemplatesClient _templates = Substitute.For<ITemplatesClient>();
    private readonly ICacheService<IMemoryCacheType> _cache = Substitute.For<ICacheService<IMemoryCacheType>>();
    private readonly ApplicationStatusService _service;

    public ApplicationStatusServiceTests()
    {
        _cache.GetOrAddAsync(
                Arg.Any<string>(),
                Arg.Any<Func<Task<ObservableCollection<CustomApplicationStatusDto>>>>(),
                Arg.Any<string>())
            .Returns(call => call.Arg<Func<Task<ObservableCollection<CustomApplicationStatusDto>>>>()());

        _service = new ApplicationStatusService(_templates, NullLogger<ApplicationStatusService>.Instance, _cache);
    }

    [Fact]
    public async Task GetCustomApplicationStatusesAsync_ShouldReturnEmpty_WhenTemplateIdIsMissing()
    {
        var result = await _service.GetCustomApplicationStatusesAsync(null);

        Assert.Empty(result);
        await _templates.DidNotReceive().GetCustomApplicationStatusesAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task GetCustomApplicationStatusesAsync_ShouldReturnStatusesFromClient()
    {
        var templateId = Guid.NewGuid();
        var statuses = new ObservableCollection<CustomApplicationStatusDto>
        {
            new() { ApplicationStatus = ApplicationStatus.InProgress, Label = "In progress" }
        };
        _templates.GetCustomApplicationStatusesAsync(templateId).Returns(statuses);

        var result = await _service.GetCustomApplicationStatusesAsync(templateId);

        Assert.Equal("In progress", result.Single().Label);
    }

    [Fact]
    public async Task GetCustomApplicationStatusesAsync_ShouldReturnEmpty_WhenClientReturnsUnauthorized()
    {
        var templateId = Guid.NewGuid();
        _templates.GetCustomApplicationStatusesAsync(templateId)
            .Throws(new ExternalApplicationsException("denied", 401, "Unauthorized", null!, null!));

        var result = await _service.GetCustomApplicationStatusesAsync(templateId);

        Assert.Empty(result);
    }

    [Fact]
    public void GetCalculatedApplicationStatusAsync_ShouldReturnSubmitted_WhenAlreadySubmitted()
    {
        var application = new ApplicationDto
        {
            ApplicationId = Guid.NewGuid(),
            Status = ApplicationStatus.Submitted
        };

        var result = _service.GetCalculatedApplicationStatusAsync(application, []);

        Assert.Equal(ApplicationStatus.Submitted, result.Key);
        Assert.Equal(GetDescription(ApplicationStatus.Submitted), result.Value);
    }

    [Fact]
    public void GetCalculatedApplicationStatusAsync_ShouldReturnInProgress_WhenResponseHasFieldData()
    {
        var json = """{"name":"Ada"}""";
        var application = new ApplicationDto
        {
            ApplicationId = Guid.NewGuid(),
            Status = ApplicationStatus.Created,
            LatestResponse = new ApplicationResponseDetailsDto
            {
                ResponseBody = Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
            }
        };

        var result = _service.GetCalculatedApplicationStatusAsync(application, []);

        Assert.Equal(ApplicationStatus.InProgress, result.Key);
    }

    [Fact]
    public void GetCalculatedApplicationStatusAsync_ShouldIgnoreTaskStatusOnlyBodies()
    {
        var application = new ApplicationDto
        {
            ApplicationId = Guid.NewGuid(),
            Status = ApplicationStatus.Created,
            LatestResponse = new ApplicationResponseDetailsDto
            {
                ResponseBody = """{"TaskStatus_t1":"Completed"}"""
            }
        };

        var result = _service.GetCalculatedApplicationStatusAsync(application, []);

        Assert.Equal(ApplicationStatus.Created, result.Key);
    }

    [Fact]
    public void GetCalculatedApplicationStatusAsync_ShouldUseCurrentStatus_WhenBodyIsInvalidJson()
    {
        var application = new ApplicationDto
        {
            ApplicationId = Guid.NewGuid(),
            Status = ApplicationStatus.Created,
            LatestResponse = new ApplicationResponseDetailsDto { ResponseBody = "not-json{" }
        };

        var result = _service.GetCalculatedApplicationStatusAsync(application, []);

        Assert.Equal(ApplicationStatus.Created, result.Key);
    }

    [Fact]
    public void GetStatusLabel_ShouldPreferCustomLabel()
    {
        var custom = new List<CustomApplicationStatusDto>
        {
            new() { ApplicationStatus = ApplicationStatus.InProgress, Label = "Working on it" }
        };

        Assert.Equal("Working on it", _service.GetStatusLabel(ApplicationStatus.InProgress, custom));
        Assert.Equal(GetDescription(ApplicationStatus.Created), _service.GetStatusLabel(ApplicationStatus.Created, custom));
    }

    [Fact]
    public void GetBaseApplicationStatuses_ShouldIncludeEveryEnumValue()
    {
        var statuses = _service.GetBaseApplicationStatuses();

        Assert.Equal(Enum.GetValues<ApplicationStatus>().Length, statuses.Count);
        Assert.All(statuses, pair => Assert.False(string.IsNullOrWhiteSpace(pair.Value)));
    }

    [Fact]
    public async Task OverrideApplicationStatusLabels_ShouldCallClient()
    {
        var templateId = Guid.NewGuid();
        var request = new CustomApplicationStatusRequest();

        await _service.OverrideApplicationStatusLabels(templateId, request);

        await _templates.Received(1).CreateCustomApplicationStatusAsync(templateId, request);
    }

    private static string GetDescription(ApplicationStatus status)
    {
        var field = status.GetType().GetField(status.ToString());
        var attributes = (DescriptionAttribute[])field!.GetCustomAttributes(typeof(DescriptionAttribute), false);
        return attributes.Length > 0 ? attributes[0].Description : status.ToString();
    }
}
