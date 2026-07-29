using GovUK.Dfe.FlexForms.Application.Exceptions;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Infrastructure.Services;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.CoreLibs.Http.Models;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Services;

public class ApplicationStateServiceTests
{
    private readonly IApplicationsClient _applicationsClient = Substitute.For<IApplicationsClient>();
    private readonly IApplicationResponseService _applicationResponseService = Substitute.For<IApplicationResponseService>();
    private readonly IFieldRequirementService _fieldRequirementService = Substitute.For<IFieldRequirementService>();

    private ApplicationStateService CreateService() =>
        new(_applicationsClient, _applicationResponseService, _fieldRequirementService, NullLogger<ApplicationStateService>.Instance);

    [Fact]
    public async Task EnsureApplicationIdAsync_AlwaysCallsApi_EvenWhenSessionHasCachedApplication()
    {
        const string reference = "APP-001";
        var applicationId = Guid.NewGuid();
        var session = CreateSession(session =>
        {
            session.SetString("ApplicationId", applicationId.ToString());
            session.SetString("ApplicationReference", reference);
            session.SetString($"TemplateSchema_{reference}", "{\"templateId\":\"t1\"}");
            session.SetString($"TemplateVersionId_{reference}", Guid.NewGuid().ToString());
        });

        var apiApplication = CreateApplication(reference, applicationId);
        _applicationsClient.GetApplicationByReferenceAsync(reference).Returns(apiApplication);

        var service = CreateService();
        var (returnedId, returnedApplication) = await service.EnsureApplicationIdAsync(reference, session);

        Assert.Equal(applicationId, returnedId);
        Assert.Same(apiApplication, returnedApplication);
        await _applicationsClient.Received(1).GetApplicationByReferenceAsync(reference);
        _applicationResponseService.Received(1).ClearAccumulatedFormData(session);
    }

    [Fact]
    public async Task EnsureApplicationIdAsync_ThrowsApplicationAccessException_WhenApiReturns404()
    {
        const string reference = "APP-MISSING";
        var session = CreateSession();

        _applicationsClient.GetApplicationByReferenceAsync(reference)
            .Throws(new ExternalApplicationsException<ExceptionResponse>(
                "Resource not found",
                404,
                "{}",
                new Dictionary<string, IEnumerable<string>>(),
                new ExceptionResponse { StatusCode = 404 },
                null));

        var service = CreateService();

        var exception = await Assert.ThrowsAsync<ApplicationAccessException>(
            () => service.EnsureApplicationIdAsync(reference, session));

        Assert.Equal(reference, exception.ApplicationReference);
    }

    [Theory]
    [InlineData("Created", true)]
    [InlineData("InProgress", true)]
    [InlineData("Submitted", false)]
    [InlineData("Deleted", false)]
    public void IsApplicationEditable_AllowsCreatedAndInProgress(string status, bool expected)
    {
        var service = CreateService();

        Assert.Equal(expected, service.IsApplicationEditable(status));
    }

    [Fact]
    public async Task EnsureApplicationIdAsync_ThrowsApplicationAccessException_WhenApiReturns403()
    {
        const string reference = "APP-FORBIDDEN";
        var session = CreateSession();

        _applicationsClient.GetApplicationByReferenceAsync(reference)
            .Throws(new ExternalApplicationsException<ExceptionResponse>(
                "Forbidden",
                403,
                "{}",
                new Dictionary<string, IEnumerable<string>>(),
                new ExceptionResponse { StatusCode = 403 },
                null));

        var service = CreateService();

        await Assert.ThrowsAsync<ApplicationAccessException>(
            () => service.EnsureApplicationIdAsync(reference, session));
    }

    [Fact]
    public async Task EnsureApplicationIdAsync_ClearsFormData_WhenReferenceChanges()
    {
        var session = CreateSession(session =>
        {
            session.SetString("ApplicationReference", "APP-OLD");
            session.SetString("ApplicationId", Guid.NewGuid().ToString());
        });

        const string newReference = "APP-NEW";
        var apiApplication = CreateApplication(newReference, Guid.NewGuid());
        _applicationsClient.GetApplicationByReferenceAsync(newReference).Returns(apiApplication);

        var service = CreateService();
        await service.EnsureApplicationIdAsync(newReference, session);

        _applicationResponseService.Received(1).ClearAccumulatedFormData(session);
        Assert.Equal(newReference, session.GetString("ApplicationReference"));
    }

    private static ApplicationDto CreateApplication(string reference, Guid applicationId) =>
        new()
        {
            ApplicationId = applicationId,
            ApplicationReference = reference,
            TemplateVersionId = Guid.NewGuid(),
            Status = ApplicationStatus.InProgress,
            TemplateSchema = new TemplateSchemaDto
            {
                JsonSchema = "{\"templateId\":\"t1\"}",
                TemplateVersionId = Guid.NewGuid(),
                TemplateId = Guid.NewGuid(),
                VersionNumber = "1.0"
            },
            CreatedBy = new UserDto
            {
                UserId = Guid.NewGuid(),
                Name = "Lead Applicant",
                Email = "lead@example.com"
            }
        };

    private static ISession CreateSession(Action<ISession>? configure = null)
    {
        var session = new TestSession();
        configure?.Invoke(session);
        return session;
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new(StringComparer.OrdinalIgnoreCase);
        private bool _isAvailable = true;

        public bool IsAvailable => _isAvailable;
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public IEnumerable<string> Keys => _store.Keys;

        public void Clear() => _store.Clear();

        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Remove(string key) => _store.Remove(key);

        public void Set(string key, byte[] value) => _store[key] = value;

        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }
}
