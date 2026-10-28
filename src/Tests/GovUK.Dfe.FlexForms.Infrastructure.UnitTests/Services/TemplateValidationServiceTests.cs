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

    [Fact]
    public void ValidateTemplateJson_ShouldPass_WhenApplicationMetaDataIsNull()
    {
        var template = CreateMinimalValidTemplate();
        template.ApplicationMetaData = null;

        var (isValid, errors) = _service.ValidateTemplateJson(JsonSerializer.Serialize(template));

        Assert.True(isValid, string.Join("; ", errors));
    }

    [Fact]
    public void ValidateTemplateJson_ShouldPass_WhenApplicationMetaDataItemsEmpty()
    {
        var template = CreateMinimalValidTemplate();
        template.ApplicationMetaData = new ApplicationMetadataConfiguration { Items = [] };

        var (isValid, errors) = _service.ValidateTemplateJson(JsonSerializer.Serialize(template));

        Assert.True(isValid, string.Join("; ", errors));
    }

    [Fact]
    public void ValidateTemplateJson_ShouldPass_WhenApplicationMetaDataItemsValid()
    {
        var template = CreateMinimalValidTemplate();
        template.ApplicationMetaData = new ApplicationMetadataConfiguration
        {
            Items =
            [
                new ApplicationMetadataItemDefinition
                {
                    Label = "Reference",
                    FieldId = "application.reference",
                    Order = 10
                }
            ]
        };

        var (isValid, errors) = _service.ValidateTemplateJson(JsonSerializer.Serialize(template));

        Assert.True(isValid, string.Join("; ", errors));
    }

    [Fact]
    public void ValidateTemplateJson_ShouldFail_WhenApplicationMetaDataExceedsMaxItems()
    {
        var template = CreateMinimalValidTemplate();
        template.ApplicationMetaData = new ApplicationMetadataConfiguration
        {
            Items = Enumerable.Range(1, ApplicationMetadataConfiguration.MaxItems + 1)
                .Select(i => new ApplicationMetadataItemDefinition
                {
                    Label = $"Row {i}",
                    FieldId = "application.reference"
                })
                .ToList()
        };

        var (isValid, errors) = _service.ValidateTemplateJson(JsonSerializer.Serialize(template));

        Assert.False(isValid);
        Assert.Contains(
            errors,
            e => e.Contains(
                $"applicationMetaData.items cannot contain more than {ApplicationMetadataConfiguration.MaxItems}",
                StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateTemplateJson_ShouldFail_WhenApplicationMetaDataItemMissingLabelOrFieldId()
    {
        var template = CreateMinimalValidTemplate();
        template.ApplicationMetaData = new ApplicationMetadataConfiguration
        {
            Items =
            [
                new ApplicationMetadataItemDefinition { Label = " ", FieldId = "application.reference" },
                new ApplicationMetadataItemDefinition { Label = "Version", FieldId = " " }
            ]
        };

        var (isValid, errors) = _service.ValidateTemplateJson(JsonSerializer.Serialize(template));

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("applicationMetaData.items[0]: 'label' is required", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("applicationMetaData.items[1]: 'fieldId' is required", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateTemplateJson_ShouldFail_WhenApplicationMetaDataFieldIdNotInTemplate()
    {
        var template = CreateMinimalValidTemplate();
        template.TaskGroups[0].Tasks[0].Pages =
        [
            new Page
            {
                PageId = "p1",
                Slug = "p1",
                Title = "Page",
                Description = "d",
                PageOrder = 1,
                Fields = [new Field { FieldId = "trustName", Type = "text", Label = new Label { Value = "Trust" }, Order = 1 }]
            }
        ];
        template.ApplicationMetaData = new ApplicationMetadataConfiguration
        {
            Items =
            [
                new ApplicationMetadataItemDefinition { Label = "Trust", FieldId = "field.trustName" },
                new ApplicationMetadataItemDefinition { Label = "Second trust", FieldId = "field.trustNmae" }
            ]
        };

        var (isValid, errors) = _service.ValidateTemplateJson(JsonSerializer.Serialize(template));

        Assert.False(isValid);
        Assert.Equal(
            "applicationMetaData.items[1] ('Second trust'): fieldId 'field.trustNmae' is invalid: "
            + "no field or collection with ID 'trustNmae' exists in the template. Did you mean 'trustName'?",
            Assert.Single(errors));
    }

    [Fact]
    public void ValidateTemplateJson_ShouldFail_WhenIdsAreDuplicated()
    {
        var template = CreateMinimalValidTemplate();
        var group = template.TaskGroups[0];
        group.Tasks[0].Pages = [CreatePage("p1", CreateField("name")), CreatePage("p1", CreateField("name"))];
        group.Tasks.Add(new TaskModel { TaskId = "t1", TaskName = "Copy", TaskOrder = 2, TaskStatusString = "NotStarted", Pages = [] });
        template.TaskGroups.Add(new TaskGroup { GroupId = "g1", GroupName = "Copy", GroupOrder = 2, GroupStatus = "NotStarted", Tasks = group.Tasks });

        var (isValid, errors) = _service.ValidateTemplateJson(JsonSerializer.Serialize(template));

        Assert.False(isValid);
        Assert.Contains("groupId 'g1' is used more than once in the template. Each groupId must be unique", errors);
        Assert.Contains("taskId 't1' is used more than once in the template. Each taskId must be unique", errors);
        Assert.Contains(errors, e => e.StartsWith("pageId 'p1' is used more than once in task 't1'", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("fieldId 'name' is used more than once in task pages and collections", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateTemplateJson_ShouldAllowSameFieldIdInDifferentCollections()
    {
        var template = CreateMinimalValidTemplate();
        template.TaskGroups[0].Tasks[0].Pages = null;
        template.TaskGroups[0].Tasks[0].Summary = new TaskSummaryConfiguration
        {
            Mode = "multiCollectionFlow",
            Flows =
            [
                new MultiCollectionFlowConfiguration { FlowId = "trusts", FieldId = "trusts", Pages = [CreatePage("trust-page", CreateField("name"))] },
                new MultiCollectionFlowConfiguration { FlowId = "academies", FieldId = "academies", Pages = [CreatePage("academy-page", CreateField("name"))] }
            ]
        };

        var (isValid, errors) = _service.ValidateTemplateJson(JsonSerializer.Serialize(template));

        Assert.True(isValid, string.Join("; ", errors));
    }

    [Theory]
    [InlineData("number", "type 'number' isn't supported")]
    [InlineData("autocomplete", "type 'autocomplete' can't be shown to users")]
    public void ValidateTemplateJson_ShouldFail_WhenFieldTypeCannotBeRendered(string type, string expected)
    {
        var template = CreateMinimalValidTemplate();
        var field = CreateField("answer");
        field.Type = type;
        template.TaskGroups[0].Tasks[0].Pages = [CreatePage("p1", field)];

        var (isValid, errors) = _service.ValidateTemplateJson(JsonSerializer.Serialize(template));

        Assert.False(isValid);
        Assert.Contains(errors, e => e.StartsWith("Field 'answer' on page 'p1' in task 't1'", StringComparison.Ordinal) && e.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateTemplateJson_ShouldFail_WhenChoiceOrComplexFieldIsIncomplete()
    {
        var template = CreateMinimalValidTemplate();
        var radios = CreateField("choice");
        radios.Type = "radios";
        var checkboxes = CreateField("many");
        checkboxes.Type = "checkboxes";
        checkboxes.Options = [new Option { Value = " ", Label = "Blank" }];
        var search = CreateField("search");
        search.Type = "complexField";
        template.TaskGroups[0].Tasks[0].Pages = [CreatePage("p1", radios, checkboxes, search)];

        var (isValid, errors) = _service.ValidateTemplateJson(JsonSerializer.Serialize(template));

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("a 'radios' question needs at least one option", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("every option needs a 'value'", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("'complexField.id' is required", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateTemplateJson_ShouldFail_WhenConditionalLogicPointsAtMissingElements()
    {
        var template = CreateMinimalValidTemplate();
        template.TaskGroups[0].Tasks[0].Pages = [CreatePage("p1", CreateField("answer"))];
        template.ConditionalLogic =
        [
            new ConditionalLogic
            {
                Id = "rule-1",
                ConditionGroup = new ConditionGroup
                {
                    LogicalOperator = "AND",
                    Conditions =
                    [
                        new Condition { TriggerField = "answer", Operator = "equals", Value = "yes" },
                        new Condition { TriggerField = "missingTrigger", Operator = "equals", Value = "yes" }
                    ]
                },
                AffectedElements =
                [
                    new AffectedElement { ElementId = "p1", ElementType = "page", Action = "show" },
                    new AffectedElement { ElementId = "missingPage", ElementType = "page", Action = "skip" },
                    new AffectedElement { ElementId = "missingField", ElementType = "field", Action = "hide" }
                ]
            }
        ];

        var (isValid, errors) = _service.ValidateTemplateJson(JsonSerializer.Serialize(template));

        Assert.False(isValid);
        Assert.Equal(
            [
                "Conditional logic rule 'rule-1': triggerField 'missingTrigger' is not a field in this template",
                "Conditional logic rule 'rule-1': page 'missingPage' is not in this template",
                "Conditional logic rule 'rule-1': field 'missingField' is not in this template"
            ],
            errors);
    }

    private static Page CreatePage(string pageId, params Field[] fields) =>
        new()
        {
            PageId = pageId,
            Slug = pageId,
            Title = "Page",
            Description = "",
            PageOrder = 1,
            Fields = [.. fields]
        };

    private static Field CreateField(string fieldId) =>
        new() { FieldId = fieldId, Type = "text", Label = new Label { Value = "Question" }, Order = 1 };

    private static FormTemplate CreateMinimalValidTemplate() =>
        new()
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
                            TaskId = "t1",
                            TaskName = "Task",
                            TaskOrder = 1,
                            TaskStatusString = "NotStarted",
                            Pages = []
                        }
                    ]
                }
            ]
        };
}
