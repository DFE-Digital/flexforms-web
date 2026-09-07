using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Domain.Templates;
using GovUK.Dfe.FlexForms.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using TaskModel = GovUK.Dfe.FlexForms.Domain.Models.Task;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Services;

public class TemplateValidationServiceTests
{
    private readonly TemplateValidationService _service = new(NullLogger<TemplateValidationService>.Instance);

    [Fact]
    public void ValidateTemplateJson_ShouldFail_WhenEmpty()
    {
        var (isValid, errors) = _service.ValidateTemplateJson(" ");

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("required"));
    }

    [Fact]
    public void ValidateTemplateJson_ShouldFail_WhenJsonIsInvalid()
    {
        var (isValid, errors) = _service.ValidateTemplateJson("{not-json");

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("JSON parsing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateTemplateJson_ShouldPass_ForStarterSchema()
    {
        var json = StarterFormTemplateSchema.CreateJson(Guid.NewGuid().ToString(), "Starter");

        var (isValid, errors) = _service.ValidateTemplateJson(json);

        Assert.True(isValid, string.Join("; ", errors));
        var (template, parseErrors) = _service.TryParseTemplate(json);
        Assert.NotNull(template);
        Assert.Empty(parseErrors);
    }

    [Fact]
    public void ValidateTemplateJson_ShouldFail_WhenTemplateNameMissing()
    {
        var json = JsonSerializer.Serialize(new FormTemplate
        {
            TemplateId = "tpl-1",
            TemplateName = " ",
            Description = "desc",
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
                        new TaskModel
                        {
                            TaskId = "t1",
                            TaskName = "Task",
                            TaskOrder = 1,
                            TaskStatusString = "NotStarted",
                            Pages = []
                        }
                    ]
                }
            ]
        });

        var (isValid, errors) = _service.ValidateTemplateJson(json);

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("templateName", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateTemplateJson_ShouldFail_WhenTaskGroupsEmpty()
    {
        var json = JsonSerializer.Serialize(new FormTemplate
        {
            TemplateId = "tpl-1",
            TemplateName = "Name",
            Description = "desc",
            TaskGroups = []
        });

        var (isValid, errors) = _service.ValidateTemplateJson(json);

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("taskGroups", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateTemplateJson_ShouldFail_WhenTemplateIdMissing()
    {
        var json = JsonSerializer.Serialize(new FormTemplate
        {
            TemplateId = "",
            TemplateName = "Name",
            Description = "desc",
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
                        new TaskModel
                        {
                            TaskId = "t1",
                            TaskName = "Task",
                            TaskOrder = 1,
                            TaskStatusString = "NotStarted",
                            Pages = []
                        }
                    ]
                }
            ]
        });

        var (isValid, errors) = _service.ValidateTemplateJson(json);

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("templateId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateTemplateJson_ShouldFail_WhenTaskGroupAndNestedFieldsInvalid()
    {
        var json = JsonSerializer.Serialize(new FormTemplate
        {
            TemplateId = "tpl-1",
            TemplateName = "Name",
            Description = "desc",
            TaskGroups =
            [
                new TaskGroup
                {
                    GroupId = "",
                    GroupName = "",
                    GroupOrder = 1,
                    GroupStatus = "NotStarted",
                    Tasks = []
                }
            ]
        });

        var (isValid, errors) = _service.ValidateTemplateJson(json);

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("groupId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("groupName", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("must have at least one task", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateTemplateJson_ShouldFail_WhenTaskPageAndFieldMetadataMissing()
    {
        var json = JsonSerializer.Serialize(new FormTemplate
        {
            TemplateId = "tpl-1",
            TemplateName = "Name",
            Description = "desc",
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
                        new TaskModel
                        {
                            TaskId = "",
                            TaskName = "",
                            TaskOrder = 1,
                            TaskStatusString = "NotStarted",
                            Pages =
                            [
                                new Page
                                {
                                    PageId = "",
                                    Slug = "page",
                                    Title = "",
                                    Description = "desc",
                                    PageOrder = 1,
                                    Fields =
                                    [
                                        new Field
                                        {
                                            FieldId = "",
                                            Type = "",
                                            Order = 1,
                                            Label = new Label { Value = "", IsVisible = true },
                                            Validations =
                                            [
                                                new ValidationRule { Type = "", Rule = "true", Message = "" }
                                            ]
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ]
        });

        var (isValid, errors) = _service.ValidateTemplateJson(json);

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("taskId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("taskName", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("pageId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("title", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("'type' is required", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("label.value", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("Validation rule", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateTemplateJson_ShouldFail_WhenFieldLabelMissing()
    {
        var json = """
                   {
                     "templateId": "tpl-1",
                     "templateName": "Name",
                     "description": "desc",
                     "taskGroups": [
                       {
                         "groupId": "g1",
                         "groupName": "Group",
                         "groupOrder": 1,
                         "groupStatus": "NotStarted",
                         "tasks": [
                           {
                             "taskId": "t1",
                             "taskName": "Task",
                             "taskOrder": 1,
                             "taskStatusString": "NotStarted",
                             "pages": [
                               {
                                 "pageId": "p1",
                                 "slug": "p1",
                                 "title": "Page",
                                 "description": "desc",
                                 "pageOrder": 1,
                                 "fields": [
                                   {
                                     "fieldId": "f1",
                                     "type": "text",
                                     "order": 1
                                   }
                                 ]
                               }
                             ]
                           }
                         ]
                       }
                     ]
                   }
                   """;

        var (isValid, errors) = _service.ValidateTemplateJson(json);

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("label", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TryParseTemplate_ShouldReturnErrors_WhenValidationFails()
    {
        var (template, errors) = _service.TryParseTemplate(" ");

        Assert.Null(template);
        Assert.NotEmpty(errors);
    }
}
