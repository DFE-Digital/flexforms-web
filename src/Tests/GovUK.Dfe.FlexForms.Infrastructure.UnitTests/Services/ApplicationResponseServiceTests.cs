using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Http.Models;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Exceptions;
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
    public void TransformToResponseJson_SerializesMultiSelectCheckboxValues_AsJsonArrayString()
    {
        var service = CreateService();

        var json = service.TransformToResponseJson(
            new Dictionary<string, object>
            {
                ["significantChangeType"] = new[] { "change-trust-name", "change-address" }
            },
            new Dictionary<string, string>());

        using var doc = JsonDocument.Parse(json);
        var value = doc.RootElement.GetProperty("significantChangeType").GetProperty("value").GetString();

        Assert.Equal("""["change-trust-name","change-address"]""", value);
    }

    [Fact]
    public void GetAccumulatedFormData_PreservesMultiSelectCheckboxValues_AfterSessionRoundTrip()
    {
        var session = CreateSessionStore();
        var service = CreateService(session);

        service.AccumulateFormData(new Dictionary<string, object>
        {
            ["significantChangeType"] = new[] { "change-trust-name", "change-address" }
        });

        var accumulated = service.GetAccumulatedFormData();

        Assert.True(accumulated.TryGetValue("significantChangeType", out var value));
        var values = Assert.IsType<string[]>(value);
        Assert.Equal(["change-trust-name", "change-address"], values);
    }

    [Fact]
    public void TransformToResponseJson_FormatsNullValue_AsEmptyString()
    {
        var service = CreateService();

        var json = service.TransformToResponseJson(
            new Dictionary<string, object> { ["field"] = null! },
            new Dictionary<string, string>());

        Assert.Equal(string.Empty, ReadStoredFieldValue(json, "field"));
    }

    [Theory]
    [InlineData("plain-text", "plain-text")]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void TransformToResponseJson_FormatsPrimitiveValues(object input, string expected)
    {
        var service = CreateService();

        var json = service.TransformToResponseJson(
            new Dictionary<string, object> { ["field"] = input },
            new Dictionary<string, string>());

        Assert.Equal(expected, ReadStoredFieldValue(json, "field"));
    }

    [Fact]
    public void TransformToResponseJson_SerializesSingleCheckboxValue_AsPlainString()
    {
        var service = CreateService();

        var json = service.TransformToResponseJson(
            new Dictionary<string, object> { ["choices"] = new[] { "only-one" } },
            new Dictionary<string, string>());

        Assert.Equal("only-one", ReadStoredFieldValue(json, "choices"));
    }

    [Fact]
    public void TransformToResponseJson_SerializesEmptyCheckboxSelection_AsEmptyString()
    {
        var service = CreateService();

        var json = service.TransformToResponseJson(
            new Dictionary<string, object> { ["choices"] = Array.Empty<string>() },
            new Dictionary<string, string>());

        Assert.Equal(string.Empty, ReadStoredFieldValue(json, "choices"));
    }

    [Fact]
    public void TransformToResponseJson_SerializesStringEnumerable_CheckboxValues()
    {
        var service = CreateService();

        var json = service.TransformToResponseJson(
            new Dictionary<string, object>
            {
                ["choices"] = new List<string> { "alpha", " ", "beta" }
            },
            new Dictionary<string, string>());

        Assert.Equal("""["alpha","beta"]""", ReadStoredFieldValue(json, "choices"));
    }

    [Fact]
    public void TransformToResponseJson_SerializesSingleItemStringEnumerable_AsPlainString()
    {
        var service = CreateService();

        var json = service.TransformToResponseJson(
            new Dictionary<string, object> { ["choices"] = new List<string> { "alpha" } },
            new Dictionary<string, string>());

        Assert.Equal("alpha", ReadStoredFieldValue(json, "choices"));
    }

    [Fact]
    public void TransformToResponseJson_SerializesDecimalUsingInvariantCulture()
    {
        var service = CreateService();

        var json = service.TransformToResponseJson(
            new Dictionary<string, object> { ["amount"] = 12.5m },
            new Dictionary<string, string>());

        Assert.Equal("12.5", ReadStoredFieldValue(json, "amount"));
    }

    [Fact]
    public void TransformToResponseJson_SerializesComplexObjectAsJson()
    {
        var service = CreateService();

        var json = service.TransformToResponseJson(
            new Dictionary<string, object>
            {
                ["establishment"] = new Dictionary<string, string>
                {
                    ["name"] = "Contoso Academy",
                    ["ukprn"] = "123456"
                }
            },
            new Dictionary<string, string>());

        using var doc = JsonDocument.Parse(ReadStoredFieldValue(json, "establishment"));
        Assert.Equal("Contoso Academy", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("123456", doc.RootElement.GetProperty("ukprn").GetString());
    }

    [Theory]
    [InlineData("\"stored\"", "stored")]
    [InlineData("[\"one\",\"two\"]", """["one","two"]""")]
    [InlineData("[\"only\"]", "only")]
    [InlineData("[]", "")]
    [InlineData("42", "42")]
    [InlineData("true", "true")]
    [InlineData("false", "false")]
    [InlineData("{\"id\":\"x\"}", "{\"id\":\"x\"}")]
    public void TransformToResponseJson_SerializesJsonElementValues(string rawJson, string expected)
    {
        using var elementDoc = JsonDocument.Parse(rawJson);
        var service = CreateService();

        var json = service.TransformToResponseJson(
            new Dictionary<string, object> { ["field"] = elementDoc.RootElement.Clone() },
            new Dictionary<string, string>());

        Assert.Equal(expected, ReadStoredFieldValue(json, "field"));
    }

    [Fact]
    public void TransformToResponseJson_SerializesMixedJsonArrayUsingRawJson()
    {
        using var elementDoc = JsonDocument.Parse("""[1,"two"]""");
        var service = CreateService();

        var json = service.TransformToResponseJson(
            new Dictionary<string, object> { ["field"] = elementDoc.RootElement.Clone() },
            new Dictionary<string, string>());

        Assert.Equal("""[1,"two"]""", ReadStoredFieldValue(json, "field"));
    }

    [Theory]
    [InlineData("null", "")]
    [InlineData("\"answer\"", "answer")]
    [InlineData("true", "true")]
    [InlineData("false", "false")]
    [InlineData("12.5", "12.5")]
    [InlineData("[\"one\"]", "one")]
    [InlineData("[\"one\",\"two\"]", null)]
    public void GetAccumulatedFormData_CleansJsonElementValues(string storedJson, string? expectedSingle)
    {
        var session = CreateSessionStore(store =>
            store.SetString(FormSessionKeys.AccumulatedFormData, $$"""{"field":{{storedJson}}}"""));
        var service = CreateService(session);

        var accumulated = service.GetAccumulatedFormData();

        Assert.True(accumulated.TryGetValue("field", out var value));
        if (expectedSingle is not null)
        {
            Assert.Equal(expectedSingle, value);
            return;
        }

        Assert.Equal(["one", "two"], Assert.IsType<string[]>(value));
    }

    [Fact]
    public void GetAccumulatedFormData_CleansJsonObjectArray_ToRawJsonString()
    {
        var session = CreateSessionStore(store =>
            store.SetString(
                FormSessionKeys.AccumulatedFormData,
                """{"files":[{"id":"f1","originalFileName":"evidence.pdf"}]}"""));
        var service = CreateService(session);

        var accumulated = service.GetAccumulatedFormData();

        Assert.True(accumulated.TryGetValue("files", out var value));
        var raw = Assert.IsType<string>(value);
        using var doc = JsonDocument.Parse(raw);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal("f1", doc.RootElement[0].GetProperty("id").GetString());
    }

    [Fact]
    public void GetAccumulatedFormData_CleansJsonObject_ToRawJsonString()
    {
        var session = CreateSessionStore(store =>
            store.SetString(
                FormSessionKeys.AccumulatedFormData,
                """{"establishment":{"name":"Contoso","ukprn":"123"}}"""));
        var service = CreateService(session);

        var accumulated = service.GetAccumulatedFormData();

        Assert.True(accumulated.TryGetValue("establishment", out var value));
        var raw = Assert.IsType<string>(value);
        using var doc = JsonDocument.Parse(raw);
        Assert.Equal("Contoso", doc.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public void GetAccumulatedFormData_CleansSingleStringArrayValue_ToPlainString()
    {
        var session = CreateSessionStore();
        var service = CreateService(session);
        service.AccumulateFormData(new Dictionary<string, object> { ["field"] = new[] { "one" } });

        var accumulated = service.GetAccumulatedFormData();

        Assert.Equal("one", accumulated["field"]);
    }

    [Fact]
    public async Task SaveApplicationResponseAsync_PersistsMultiSelectCheckboxValues_InResponseBody()
    {
        var session = CreateSessionStore();
        var applications = Substitute.For<IApplicationsClient>();
        AddApplicationResponseRequest? capturedRequest = null;
        applications
            .When(x => x.AddApplicationResponseAsync(Arg.Any<Guid>(), Arg.Any<AddApplicationResponseRequest>(), Arg.Any<CancellationToken>()))
            .Do(call => capturedRequest = call.Arg<AddApplicationResponseRequest>());

        var service = CreateService(session, applications);
        var applicationId = Guid.NewGuid();
        service.SetCurrentAccumulatedApplicationId(applicationId);

        await service.SaveApplicationResponseAsync(
            applicationId,
            new Dictionary<string, object>
            {
                ["significantChangeType"] = new[] { "change-trust-name", "change-address" }
            });

        Assert.NotNull(capturedRequest);
        var decodedJson = Encoding.UTF8.GetString(Convert.FromBase64String(capturedRequest!.ResponseBody));
        using var doc = JsonDocument.Parse(decodedJson);
        Assert.Equal(
            """["change-trust-name","change-address"]""",
            doc.RootElement.GetProperty("significantChangeType").GetProperty("value").GetString());
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
        service.SetCurrentAccumulatedApplicationId(applicationId);
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
        var applicationId = Guid.NewGuid();
        service.SetCurrentAccumulatedApplicationId(applicationId);

        await service.SaveApplicationResponseAsync(
            applicationId,
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
        var applicationId = Guid.NewGuid();
        service.SetCurrentAccumulatedApplicationId(applicationId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveApplicationResponseAsync(
                applicationId,
                new Dictionary<string, object> { ["fieldOne"] = "value" }));
    }

    [Fact]
    public async Task SaveApplicationResponseAsync_ShouldRefuseSave_WhenSessionBelongsToAnotherApplication()
    {
        var session = CreateSessionStore(store =>
            store.SetString(FormSessionKeys.AccumulatedFormData, "{\"otherAppField\":\"other\"}"));
        var applications = Substitute.For<IApplicationsClient>();
        var service = CreateService(session, applications);
        var otherApplicationId = Guid.NewGuid();
        service.SetCurrentAccumulatedApplicationId(otherApplicationId);
        var applicationId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<StaleFormSessionException>(() =>
            service.SaveApplicationResponseAsync(
                applicationId,
                new Dictionary<string, object> { ["fieldOne"] = "value" }));

        Assert.Equal(applicationId, ex.ApplicationId);
        Assert.Equal(otherApplicationId.ToString(), ex.SessionApplicationId);
        await applications.DidNotReceive().AddApplicationResponseAsync(
            Arg.Any<Guid>(),
            Arg.Any<AddApplicationResponseRequest>(),
            Arg.Any<CancellationToken>());
        Assert.Equal("{\"otherAppField\":\"other\"}", session.GetString(FormSessionKeys.AccumulatedFormData));
    }

    [Fact]
    public async Task SaveApplicationResponseAsync_ShouldRefuseSave_WhenSessionHasNoOwner()
    {
        var applications = Substitute.For<IApplicationsClient>();
        var service = CreateService(CreateSessionStore(), applications);

        await Assert.ThrowsAsync<StaleFormSessionException>(() =>
            service.SaveApplicationResponseAsync(
                Guid.NewGuid(),
                new Dictionary<string, object> { ["fieldOne"] = "value" }));

        await applications.DidNotReceive().AddApplicationResponseAsync(
            Arg.Any<Guid>(),
            Arg.Any<AddApplicationResponseRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void GetAccumulatedFormData_ShouldRemoveInfectedUploads_AndCheckOnlyUploadsInOneCall()
    {
        var infectedId = Guid.NewGuid();
        var cleanId = Guid.NewGuid();
        var listItemId = Guid.NewGuid();
        var answers = new Dictionary<string, object>
        {
            ["evidence"] = JsonSerializer.Serialize(new object[]
            {
                new { id = infectedId, originalFileName = "bad.pdf", fileSize = 10 },
                new { id = cleanId, originalFileName = "good.pdf", fileSize = 10 }
            }),
            ["people"] = JsonSerializer.Serialize(new object[] { new { id = listItemId, name = "Ann" } }),
            ["colours"] = new[] { "red", "blue" }
        };
        var session = CreateSessionStore(store =>
        {
            store.SetString(FormSessionKeys.AccumulatedFormData, JsonSerializer.Serialize(answers));
            store.SetString(FormSessionKeys.ApplicationId, "app-1");
        });
        var infectedFiles = Substitute.For<IInfectedFileStore>();
        infectedFiles.FindInfectedFiles(Arg.Any<string?>(), Arg.Any<IReadOnlyCollection<InfectedFileCandidate>>())
            .Returns(new HashSet<Guid> { infectedId });
        var service = CreateService(session, infectedFileStore: infectedFiles);

        var data = service.GetAccumulatedFormData();

        var evidence = data["evidence"].ToString()!;
        Assert.DoesNotContain(infectedId.ToString(), evidence);
        Assert.Contains(cleanId.ToString(), evidence);
        Assert.Contains(listItemId.ToString(), data["people"].ToString());
        Assert.Equal(new[] { "red", "blue" }, Assert.IsType<string[]>(data["colours"]));
        infectedFiles.Received(1).FindInfectedFiles(
            "app-1",
            Arg.Is<IReadOnlyCollection<InfectedFileCandidate>>(c =>
                c.Count == 2 && c.All(f => f.FileId == infectedId || f.FileId == cleanId)));
        infectedFiles.DidNotReceive().IsFileInfected(Arg.Any<Guid>());
    }

    [Fact]
    public void GetAccumulatedFormData_ShouldReuseFilteredAnswers_UntilSessionAnswersChange()
    {
        var upload = JsonSerializer.Serialize(new object[] { new { id = Guid.NewGuid(), originalFileName = "a.pdf" } });
        var session = CreateSessionStore(store =>
            store.SetString(
                FormSessionKeys.AccumulatedFormData,
                JsonSerializer.Serialize(new Dictionary<string, object> { ["evidence"] = upload })));
        var infectedFiles = Substitute.For<IInfectedFileStore>();
        infectedFiles.FindInfectedFiles(Arg.Any<string?>(), Arg.Any<IReadOnlyCollection<InfectedFileCandidate>>())
            .Returns(new HashSet<Guid>());
        var service = CreateService(session, infectedFileStore: infectedFiles);

        var first = service.GetAccumulatedFormData();
        first["evidence"] = "changed by caller";
        var second = service.GetAccumulatedFormData();

        Assert.NotEqual("changed by caller", second["evidence"]);
        infectedFiles.ReceivedWithAnyArgs(1).FindInfectedFiles(default, default!);

        service.AccumulateFormData(new Dictionary<string, object> { ["name"] = "Ann" });
        var third = service.GetAccumulatedFormData();

        Assert.Equal("Ann", third["name"]);
        infectedFiles.ReceivedWithAnyArgs(2).FindInfectedFiles(default, default!);
    }

    private static string ReadStoredFieldValue(string responseJson, string fieldId)
    {
        using var doc = JsonDocument.Parse(responseJson);
        return doc.RootElement.GetProperty(fieldId).GetProperty("value").GetString() ?? string.Empty;
    }

    private static ApplicationResponseService CreateService(
        IFormSessionStore? sessionStore = null,
        IApplicationsClient? applicationsClient = null,
        IInfectedFileStore? infectedFileStore = null)
    {
        var infectedFiles = infectedFileStore ?? Substitute.For<IInfectedFileStore>();
        if (infectedFileStore is null)
        {
            infectedFiles.FindInfectedFiles(Arg.Any<string?>(), Arg.Any<IReadOnlyCollection<InfectedFileCandidate>>())
                .Returns(new HashSet<Guid>());
        }

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
