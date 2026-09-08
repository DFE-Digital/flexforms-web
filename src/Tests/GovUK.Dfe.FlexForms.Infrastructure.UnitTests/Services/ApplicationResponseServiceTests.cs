using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Http.Models;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Caching;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Text;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Services;

public class ApplicationResponseServiceTests
{
    [Fact]
    public void TransformToResponseJson_DoesNotMarkFieldCompleted_WhenValueIsPresentButTaskIsNotComplete()
    {
        var service = CreateService();
        var template = BuildVisitDateTemplate();

        var json = service.TransformToResponseJson(
            new Dictionary<string, object> { ["dateVisited"] = "2006-02-23" },
            new Dictionary<string, string>(),
            template);

        using var doc = JsonDocument.Parse(json);
        var field = doc.RootElement.GetProperty("dateVisited");

        Assert.Equal("2006-02-23", field.GetProperty("value").GetString());
        Assert.False(field.GetProperty("completed").GetBoolean());
        Assert.Equal("Visit date", field.GetProperty("question").GetString());
        Assert.Equal("DateTime", field.GetProperty("dataType").GetString());
    }

    [Fact]
    public void TransformToResponseJson_MarksFieldCompleted_OnlyWhenTaskStatusIsCompleted()
    {
        var service = CreateService();
        var template = BuildVisitDateTemplate();

        var json = service.TransformToResponseJson(
            new Dictionary<string, object> { ["dateVisited"] = "2006-02-23" },
            new Dictionary<string, string> { ["date-of-the-visit"] = nameof(Domain.Models.TaskStatus.Completed) },
            template);

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("dateVisited").GetProperty("completed").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("TaskStatus_date-of-the-visit").GetProperty("completed").GetBoolean());
        Assert.Equal("Completed", doc.RootElement.GetProperty("TaskStatus_date-of-the-visit").GetProperty("value").GetString());
    }

    [Fact]
    public void TransformToResponseJson_TaskStatusWrapperCompleted_IsFalse_WhenTaskIsInProgress()
    {
        var service = CreateService();
        var template = BuildVisitDateTemplate();

        var json = service.TransformToResponseJson(
            new Dictionary<string, object> { ["dateVisited"] = "2006-02-23" },
            new Dictionary<string, string> { ["date-of-the-visit"] = nameof(Domain.Models.TaskStatus.InProgress) },
            template);

        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("dateVisited").GetProperty("completed").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("TaskStatus_date-of-the-visit").GetProperty("completed").GetBoolean());
        Assert.Equal("InProgress", doc.RootElement.GetProperty("TaskStatus_date-of-the-visit").GetProperty("value").GetString());
    }

    [Fact]
    public void AccumulateFormData_ShouldStripDataPrefixAndRemoveDuplicateKeys()
    {
        var session = CreateSessionStore();
        var service = CreateService(session);

        service.AccumulateFormData(new Dictionary<string, object>
        {
            ["Data_fieldOne"] = "first"
        });
        service.AccumulateFormData(new Dictionary<string, object>
        {
            ["fieldone"] = "second"
        });

        var accumulated = service.GetAccumulatedFormData();

        Assert.Single(accumulated);
        Assert.Equal("second", accumulated.Values.Single());
        Assert.DoesNotContain(accumulated.Keys, key => key.StartsWith("Data_", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetAccumulatedFormData_ShouldReturnEmptyDictionary_WhenSessionIsEmpty()
    {
        var service = CreateService(CreateSessionStore());

        var accumulated = service.GetAccumulatedFormData();

        Assert.Empty(accumulated);
    }

    [Fact]
    public void GetAccumulatedFormData_ShouldReturnEmptyDictionary_WhenSessionJsonIsInvalid()
    {
        var session = CreateSessionStore(store =>
            store.SetString(FormSessionKeys.AccumulatedFormData, "{ not-json"));
        var service = CreateService(session);

        var accumulated = service.GetAccumulatedFormData();

        Assert.Empty(accumulated);
    }

    [Fact]
    public void ClearAccumulatedFormData_ShouldRemoveSessionValue()
    {
        var session = CreateSessionStore(store =>
            store.SetString(FormSessionKeys.AccumulatedFormData, "{\"field\":\"value\"}"));
        var service = CreateService(session);

        service.ClearAccumulatedFormData();

        Assert.Null(session.GetString(FormSessionKeys.AccumulatedFormData));
    }

    [Fact]
    public void SaveTaskStatusToSession_AndGetTaskStatusFromSession_ShouldRoundTrip()
    {
        var session = CreateSessionStore();
        var service = CreateService(session);
        var applicationId = Guid.NewGuid();

        service.SaveTaskStatusToSession(applicationId, "task-1", nameof(Domain.Models.TaskStatus.Completed));

        var taskStatus = service.GetTaskStatusFromSession(applicationId);

        Assert.Equal(nameof(Domain.Models.TaskStatus.Completed), taskStatus["task-1"]);
    }

    [Fact]
    public void StoreFormDataInSession_ShouldReplaceExistingAccumulatedData()
    {
        var session = CreateSessionStore(store =>
            store.SetString(FormSessionKeys.AccumulatedFormData, "{\"oldField\":\"old\"}"));
        var service = CreateService(session);

        service.StoreFormDataInSession(new Dictionary<string, object>
        {
            ["newField"] = "new"
        });

        var accumulated = service.GetAccumulatedFormData();

        Assert.Single(accumulated);
        Assert.Equal("new", accumulated["newField"]);
        Assert.DoesNotContain(accumulated, kvp => kvp.Key == "oldField");
    }

    [Fact]
    public void SetCurrentAccumulatedApplicationId_ShouldPersistApplicationIdInSession()
    {
        var session = CreateSessionStore();
        var service = CreateService(session);
        var applicationId = Guid.NewGuid();

        service.SetCurrentAccumulatedApplicationId(applicationId);

        Assert.Equal(applicationId.ToString(), session.GetString(FormSessionKeys.CurrentAccumulatedApplicationId));
    }

    [Fact]
    public async Task SaveApplicationResponseAsync_ShouldPersistResponseAndPromoteStatus()
    {
        var session = CreateSessionStore();
        var applications = Substitute.For<IApplicationsClient>();
        var infectedFiles = Substitute.For<IInfectedFileStore>();
        infectedFiles.IsFileInfected(Arg.Any<Guid>()).Returns(false);
        infectedFiles.IsFileNameInfected(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var templates = Substitute.For<IFormTemplateProvider>();
        var service = new ApplicationResponseService(
            applications,
            infectedFiles,
            session,
            templates,
            NullLogger<ApplicationResponseService>.Instance);

        var applicationId = Guid.NewGuid();
        session.SetString($"ApplicationStatus_{applicationId}", "Created");

        await service.SaveApplicationResponseAsync(
            applicationId,
            new Dictionary<string, object> { ["fieldOne"] = "value" });

        await applications.Received(1).AddApplicationResponseAsync(
            applicationId,
            Arg.Any<AddApplicationResponseRequest>(),
            Arg.Any<CancellationToken>());
        Assert.Equal("InProgress", session.GetString($"ApplicationStatus_{applicationId}"));
    }

    [Fact]
    public async Task SaveApplicationResponseAsync_ShouldIgnoreExternalApplicationsException_WhenStatusIs200()
    {
        var session = CreateSessionStore();
        var applications = Substitute.For<IApplicationsClient>();
        applications.AddApplicationResponseAsync(
                Arg.Any<Guid>(),
                Arg.Any<AddApplicationResponseRequest>(),
                Arg.Any<CancellationToken>())
            .Throws(new ExternalApplicationsException("saved", 200, "ok", null!, null!));
        var service = CreateService(session, applications);

        await service.SaveApplicationResponseAsync(
            Guid.NewGuid(),
            new Dictionary<string, object> { ["fieldOne"] = "value" });
    }

    [Fact]
    public async Task SaveApplicationResponseAsync_ShouldThrow_WhenApiFails()
    {
        var session = CreateSessionStore();
        var applications = Substitute.For<IApplicationsClient>();
        applications.AddApplicationResponseAsync(
                Arg.Any<Guid>(),
                Arg.Any<AddApplicationResponseRequest>(),
                Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("save failed"));
        var service = CreateService(session, applications);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveApplicationResponseAsync(
                Guid.NewGuid(),
                new Dictionary<string, object> { ["fieldOne"] = "value" }));
    }

    private static ApplicationResponseService CreateService(
        IFormSessionStore? sessionStore = null,
        IApplicationsClient? applicationsClient = null)
    {
        var infectedFiles = Substitute.For<IInfectedFileStore>();
        infectedFiles.IsFileInfected(Arg.Any<Guid>()).Returns(false);
        infectedFiles.IsFileNameInfected(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        return new ApplicationResponseService(
            applicationsClient ?? Substitute.For<IApplicationsClient>(),
            infectedFiles,
            sessionStore ?? CreateSessionStore(),
            Substitute.For<IFormTemplateProvider>(),
            NullLogger<ApplicationResponseService>.Instance);
    }

    private static InMemoryFormSessionStore CreateSessionStore(Action<IFormSessionStore>? configure = null)
    {
        var store = new InMemoryFormSessionStore();
        configure?.Invoke(store);
        return store;
    }

    private static FormTemplate BuildVisitDateTemplate() =>
        new()
        {
            TemplateId = "visits",
            TemplateName = "Visits",
            Description = "Visits",
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
                            TaskId = "date-of-the-visit",
                            TaskName = "Date of the visit",
                            TaskOrder = 1,
                            TaskStatusString = "NotStarted",
                            Pages =
                            [
                                new Page
                                {
                                    PageId = "date-of-the-visit",
                                    Slug = "date-of-the-visit",
                                    Title = "Date of the visit",
                                    Description = "desc",
                                    PageOrder = 1,
                                    Fields =
                                    [
                                        new Field
                                        {
                                            FieldId = "dateVisited",
                                            Type = "date",
                                            Order = 1,
                                            Label = new Label
                                            {
                                                Value = "Visit date",
                                                IsVisible = true
                                            }
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

    private sealed class InMemoryFormSessionStore : IFormSessionStore
    {
        private readonly Dictionary<string, string> _store = new(StringComparer.OrdinalIgnoreCase);

        public string? GetString(string key) => _store.TryGetValue(key, out var value) ? value : null;

        public void SetString(string key, string value) => _store[key] = value;

        public void Remove(string key) => _store.Remove(key);

        public IReadOnlyCollection<string> Keys => _store.Keys.ToList();
    }
}
