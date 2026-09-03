using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Text.Json;

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

    private static ApplicationResponseService CreateService() =>
        new(
            Substitute.For<IApplicationsClient>(),
            Substitute.For<IInfectedFileStore>(),
            Substitute.For<IFormSessionStore>(),
            Substitute.For<IFormTemplateProvider>(),
            NullLogger<ApplicationResponseService>.Instance);

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
}
