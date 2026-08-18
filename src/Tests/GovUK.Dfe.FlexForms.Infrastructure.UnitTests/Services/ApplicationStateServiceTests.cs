using GovUK.Dfe.FlexForms.Application.Exceptions;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Infrastructure.Services;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.CoreLibs.Http.Models;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Services;

public class ApplicationStateServiceTests
{
    private readonly IApplicationsClient _applicationsClient = Substitute.For<IApplicationsClient>();
    private readonly IApplicationResponseService _applicationResponseService = Substitute.For<IApplicationResponseService>();
    private readonly IFieldRequirementService _fieldRequirementService = Substitute.For<IFieldRequirementService>();

    private ApplicationStateService CreateService(IFormSessionStore sessionStore) =>
        new(_applicationsClient, _applicationResponseService, _fieldRequirementService, sessionStore, NullLogger<ApplicationStateService>.Instance);

    [Fact]
    public async Task EnsureApplicationIdAsync_AlwaysCallsApi_EvenWhenSessionHasCachedApplication()
    {
        const string reference = "APP-001";
        var applicationId = Guid.NewGuid();
        var sessionStore = CreateSessionStore(store =>
        {
            store.SetString("ApplicationId", applicationId.ToString());
            store.SetString("ApplicationReference", reference);
            store.SetString($"TemplateSchema_{reference}", "{\"templateId\":\"t1\"}");
            store.SetString($"TemplateVersionId_{reference}", Guid.NewGuid().ToString());
        });

        var apiApplication = CreateApplication(reference, applicationId);
        _applicationsClient.GetApplicationByReferenceAsync(reference).Returns(apiApplication);

        var service = CreateService(sessionStore);
        var (returnedId, returnedApplication) = await service.EnsureApplicationIdAsync(reference);

        Assert.Equal(applicationId, returnedId);
        Assert.Same(apiApplication, returnedApplication);
        await _applicationsClient.Received(1).GetApplicationByReferenceAsync(reference);
        _applicationResponseService.Received(1).ClearAccumulatedFormData();
    }

    [Fact]
    public async Task EnsureApplicationIdAsync_ThrowsApplicationAccessException_WhenApiReturns404()
    {
        const string reference = "APP-MISSING";
        var sessionStore = CreateSessionStore();

        _applicationsClient.GetApplicationByReferenceAsync(reference)
            .Throws(new ExternalApplicationsException<ExceptionResponse>(
                "Resource not found",
                404,
                "{}",
                new Dictionary<string, IEnumerable<string>>(),
                new ExceptionResponse { StatusCode = 404 },
                null));

        var service = CreateService(sessionStore);

        var exception = await Assert.ThrowsAsync<ApplicationAccessException>(
            () => service.EnsureApplicationIdAsync(reference));

        Assert.Equal(reference, exception.ApplicationReference);
    }

    [Theory]
    [InlineData("Created", true)]
    [InlineData("InProgress", true)]
    [InlineData("Submitted", false)]
    [InlineData("Deleted", false)]
    public void IsApplicationEditable_AllowsCreatedAndInProgress(string status, bool expected)
    {
        var service = CreateService(CreateSessionStore());

        Assert.Equal(expected, service.IsApplicationEditable(status));
    }

    [Fact]
    public async Task EnsureApplicationIdAsync_ThrowsApplicationAccessException_WhenApiReturns403()
    {
        const string reference = "APP-FORBIDDEN";
        var sessionStore = CreateSessionStore();

        _applicationsClient.GetApplicationByReferenceAsync(reference)
            .Throws(new ExternalApplicationsException<ExceptionResponse>(
                "Forbidden",
                403,
                "{}",
                new Dictionary<string, IEnumerable<string>>(),
                new ExceptionResponse { StatusCode = 403 },
                null));

        var service = CreateService(sessionStore);

        await Assert.ThrowsAsync<ApplicationAccessException>(
            () => service.EnsureApplicationIdAsync(reference));
    }

    [Fact]
    public async Task EnsureApplicationIdAsync_ClearsFormData_WhenReferenceChanges()
    {
        var sessionStore = CreateSessionStore(store =>
        {
            store.SetString("ApplicationReference", "APP-OLD");
            store.SetString("ApplicationId", Guid.NewGuid().ToString());
        });

        const string newReference = "APP-NEW";
        var apiApplication = CreateApplication(newReference, Guid.NewGuid());
        _applicationsClient.GetApplicationByReferenceAsync(newReference).Returns(apiApplication);

        var service = CreateService(sessionStore);
        await service.EnsureApplicationIdAsync(newReference);

        _applicationResponseService.Received(2).ClearAccumulatedFormData();
        Assert.Equal(newReference, sessionStore.GetString("ApplicationReference"));
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

    private static InMemoryFormSessionStore CreateSessionStore(Action<IFormSessionStore>? configure = null)
    {
        var store = new InMemoryFormSessionStore();
        configure?.Invoke(store);
        return store;
    }

    private sealed class InMemoryFormSessionStore : IFormSessionStore
    {
        private readonly Dictionary<string, string> _store = new(StringComparer.OrdinalIgnoreCase);

        public string? GetString(string key) => _store.TryGetValue(key, out var value) ? value : null;

        public void SetString(string key, string value) => _store[key] = value;

        public void Remove(string key) => _store.Remove(key);

        public IReadOnlyCollection<string> Keys => _store.Keys.ToList();
    }
}
