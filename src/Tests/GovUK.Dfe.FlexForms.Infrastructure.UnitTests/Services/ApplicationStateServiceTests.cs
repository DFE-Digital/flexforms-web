using GovUK.Dfe.FlexForms.Application.Exceptions;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Caching;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Infrastructure.Services;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.CoreLibs.Http.Models;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Task = System.Threading.Tasks.Task;

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

    [Fact]
    public void AreAllTasksCompleted_ReturnsTrue_WhenEveryTaskIsCompleted()
    {
        var applicationId = Guid.NewGuid();
        var sessionStore = CreateSessionStore(store =>
        {
            store.SetString($"TaskStatus_{applicationId}_task-1", Domain.Models.TaskStatus.Completed.ToString());
            store.SetString($"TaskStatus_{applicationId}_task-2", Domain.Models.TaskStatus.Completed.ToString());
        });
        var template = CreateTemplate("task-1", "task-2");
        var service = CreateService(sessionStore);

        var allCompleted = service.AreAllTasksCompleted(
            template,
            [],
            applicationId,
            "InProgress");

        Assert.True(allCompleted);
    }

    [Fact]
    public void AreAllTasksCompleted_ReturnsFalse_WhenAnyTaskIsNotCompleted()
    {
        var applicationId = Guid.NewGuid();
        var sessionStore = CreateSessionStore();
        var template = CreateTemplate("task-1", "task-2");
        var service = CreateService(sessionStore);

        var allCompleted = service.AreAllTasksCompleted(
            template,
            new Dictionary<string, object> { ["field-task-1"] = "started" },
            applicationId,
            "InProgress");

        Assert.False(allCompleted);
    }

    [Fact]
    public void AreAllTasksCompleted_ReturnsTrue_WhenApplicationIsSubmitted()
    {
        var template = CreateTemplate("task-1", "task-2");
        var service = CreateService(CreateSessionStore());

        var allCompleted = service.AreAllTasksCompleted(template, [], null, "Submitted");

        Assert.True(allCompleted);
    }

    [Fact]
    public void ValidateAllRequiredFieldsForSubmission_ReturnsMissingFieldsByTask()
    {
        var template = CreateTemplate("task-1");
        _fieldRequirementService
            .GetMissingRequiredFields(
                Arg.Any<Domain.Models.Task>(),
                template,
                Arg.Any<Dictionary<string, object>>(),
                Arg.Any<Func<string, bool>?>())
            .Returns(["missing-field"]);

        var service = CreateService(CreateSessionStore());
        var hidden = new Func<string, bool>(fieldId => fieldId == "hidden-field");

        var missing = service.ValidateAllRequiredFieldsForSubmission(
            template,
            [],
            hidden);

        Assert.True(missing.ContainsKey("task-1"));
        Assert.Equal(["missing-field"], missing["task-1"]);
        _fieldRequirementService.Received(1).GetMissingRequiredFields(
            Arg.Is<Domain.Models.Task>(t => t.TaskId == "task-1"),
            template,
            Arg.Any<Dictionary<string, object>>(),
            hidden);
    }

    [Fact]
    public void ValidateAllRequiredFieldsForSubmission_ReturnsEmpty_WhenTemplateHasNoTaskGroups()
    {
        var service = CreateService(CreateSessionStore());
        var template = new FormTemplate
        {
            TemplateId = "t1",
            TemplateName = "Template",
            Description = "Template",
            TaskGroups = null!
        };

        var missing = service.ValidateAllRequiredFieldsForSubmission(template, []);

        Assert.Empty(missing);
    }

    [Fact]
    public async Task SaveTaskStatusAsync_PersistsStatusAndSavesResponse()
    {
        var applicationId = Guid.NewGuid();
        var service = CreateService(CreateSessionStore());

        await service.SaveTaskStatusAsync(applicationId, "task-1", Domain.Models.TaskStatus.Completed);

        _applicationResponseService.Received(1).SaveTaskStatusToSession(
            applicationId,
            "task-1",
            Domain.Models.TaskStatus.Completed.ToString());
        await _applicationResponseService.Received(1).SaveApplicationResponseAsync(
            applicationId,
            Arg.Is<Dictionary<string, object>>(d => d.Count == 0));
    }

    [Fact]
    public void CalculateTaskStatus_ReturnsCompleted_WhenExplicitSessionStatusIsCompleted()
    {
        var applicationId = Guid.NewGuid();
        var sessionStore = CreateSessionStore(store =>
            store.SetString($"TaskStatus_{applicationId}_task-1", Domain.Models.TaskStatus.Completed.ToString()));
        var template = CreateTemplateWithField("task-1", "field-1");
        var service = CreateService(sessionStore);

        var status = service.CalculateTaskStatus("task-1", template, [], applicationId, "InProgress");

        Assert.Equal(Domain.Models.TaskStatus.Completed, status);
    }

    [Fact]
    public void CalculateTaskStatus_ReturnsInProgress_WhenTaskFieldHasValue()
    {
        var template = CreateTemplateWithField("task-1", "field-1");
        var service = CreateService(CreateSessionStore());

        var status = service.CalculateTaskStatus(
            "task-1",
            template,
            new Dictionary<string, object> { ["field-1"] = "value" },
            null,
            "InProgress");

        Assert.Equal(Domain.Models.TaskStatus.InProgress, status);
    }

    [Fact]
    public void CalculateTaskStatus_ReturnsNotStarted_WhenTaskNotFound()
    {
        var service = CreateService(CreateSessionStore());

        var status = service.CalculateTaskStatus("missing", CreateTemplate("task-1"), [], null, "InProgress");

        Assert.Equal(Domain.Models.TaskStatus.NotStarted, status);
    }

    [Fact]
    public void GetApplicationStatus_ReturnsStoredStatus()
    {
        var applicationId = Guid.NewGuid();
        var sessionStore = CreateSessionStore(store =>
            store.SetString($"ApplicationStatus_{applicationId}", "Submitted"));
        var service = CreateService(sessionStore);

        Assert.Equal("Submitted", service.GetApplicationStatus(applicationId));
    }

    [Fact]
    public void GetApplicationStatus_ReturnsInProgress_WhenNoApplicationId()
    {
        var service = CreateService(CreateSessionStore());

        Assert.Equal("InProgress", service.GetApplicationStatus(null));
    }

    [Theory]
    [InlineData("\"hello\"", "hello")]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void GetJsonElementValue_ParsesPrimitiveValues(string json, object expected)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var service = CreateService(CreateSessionStore());

        var value = service.GetJsonElementValue(doc.RootElement);

        Assert.Equal(expected, value);
    }

    [Fact]
    public void GetJsonElementValue_ParsesNumbersAsDecimal()
    {
        using var doc = System.Text.Json.JsonDocument.Parse("42");
        var service = CreateService(CreateSessionStore());

        Assert.Equal(42m, service.GetJsonElementValue(doc.RootElement));
    }

    [Fact]
    public void GetJsonElementValue_ReturnsNull_ForJsonNull()
    {
        using var doc = System.Text.Json.JsonDocument.Parse("null");
        var service = CreateService(CreateSessionStore());

        Assert.Null(service.GetJsonElementValue(doc.RootElement));
    }

    [Fact]
    public async Task LoadResponseDataIntoSessionAsync_RestoresTaskStatusAndFormData()
    {
        var applicationId = Guid.NewGuid();
        var application = new ApplicationDto
        {
            ApplicationId = applicationId,
            ApplicationReference = "APP-100",
            TemplateVersionId = Guid.NewGuid(),
            LatestResponse = new ApplicationResponseDetailsDto
            {
                ResponseId = Guid.NewGuid(),
                ResponseBody = """
                    {
                      "field-1": {"value": "answer"},
                      "TaskStatus_task-1": {"value": "Completed"}
                    }
                    """,
                CreatedOn = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            }
        };
        var service = CreateService(CreateSessionStore());

        await service.LoadResponseDataIntoSessionAsync(application);

        _applicationResponseService.Received(1).SaveTaskStatusToSession(applicationId, "task-1", "Completed");
        _applicationResponseService.Received(1).StoreFormDataInSession(
            Arg.Is<Dictionary<string, object>>(d => d.ContainsKey("field-1") && (string?)d["field-1"] == "answer"));
        _applicationResponseService.Received(1).SetCurrentAccumulatedApplicationId(applicationId);
    }

    [Fact]
    public async Task LoadResponseDataIntoSessionAsync_ClearsData_WhenNoLatestResponse()
    {
        var applicationId = Guid.NewGuid();
        var application = new ApplicationDto
        {
            ApplicationId = applicationId,
            ApplicationReference = "APP-101",
            TemplateVersionId = Guid.NewGuid(),
            LatestResponse = null
        };
        var service = CreateService(CreateSessionStore());

        await service.LoadResponseDataIntoSessionAsync(application);

        _applicationResponseService.Received(1).ClearAccumulatedFormData();
        _applicationResponseService.Received(1).SetCurrentAccumulatedApplicationId(applicationId);
    }

    private static FormTemplate CreateTemplate(params string[] taskIds) =>
        new()
        {
            TemplateId = "t1",
            TemplateName = "Template",
            Description = "Template",
            TaskGroups =
            [
                new TaskGroup
                {
                    GroupId = "g1",
                    GroupName = "Group",
                    GroupOrder = 1,
                    GroupStatus = "NotStarted",
                    Tasks = taskIds.Select((taskId, index) => new Domain.Models.Task
                    {
                        TaskId = taskId,
                        TaskName = taskId,
                        TaskOrder = index + 1,
                        TaskStatusString = "NotStarted",
                        Pages = []
                    }).ToList()
                }
            ]
        };

    private static FormTemplate CreateTemplateWithField(string taskId, string fieldId) =>
        new()
        {
            TemplateId = "t1",
            TemplateName = "Template",
            Description = "Template",
            TaskGroups =
            [
                new TaskGroup
                {
                    GroupId = "g1",
                    GroupName = "Group",
                    GroupOrder = 1,
                    GroupStatus = "NotStarted",
                    Tasks =
                    [
                        new Domain.Models.Task
                        {
                            TaskId = taskId,
                            TaskName = taskId,
                            TaskOrder = 1,
                            TaskStatusString = "NotStarted",
                            Pages =
                            [
                                new Page
                                {
                                    PageId = "p1",
                                    Slug = "p1",
                                    Title = "Page",
                                    Description = string.Empty,
                                    PageOrder = 1,
                                    Fields =
                                    [
                                        new Field
                                        {
                                            FieldId = fieldId,
                                            Type = "text",
                                            Label = new Label { Value = fieldId },
                                            Order = 1
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

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
