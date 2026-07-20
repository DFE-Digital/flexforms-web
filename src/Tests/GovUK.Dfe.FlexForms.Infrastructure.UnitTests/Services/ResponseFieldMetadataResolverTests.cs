using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Infrastructure.Services;
using System.Text.Json;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Services;

public class ResponseFieldMetadataResolverTests
{
    [Fact]
    public void ResolveQuestion_UsesLabelWhenPresent_RegardlessOfVisibility()
    {
        var template = BuildTemplate(
            pageTitle: "Page title",
            fieldId: "field1",
            fieldType: "text",
            labelValue: "What is your name?",
            labelVisible: false);

        var lookup = ResponseFieldMetadataResolver.BuildLookup(template);

        Assert.Equal("What is your name?", ResponseFieldMetadataResolver.ResolveQuestion("field1", lookup));
    }

    [Fact]
    public void ResolveQuestion_FallsBackToPageTitle_WhenLabelEmpty()
    {
        var template = BuildTemplate(
            pageTitle: "About the academy",
            fieldId: "field1",
            fieldType: "text",
            labelValue: "   ",
            labelVisible: true);

        var lookup = ResponseFieldMetadataResolver.BuildLookup(template);

        Assert.Equal("About the academy", ResponseFieldMetadataResolver.ResolveQuestion("field1", lookup));
    }

    [Fact]
    public void ResolveDataType_UsesTemplateType_ForDate()
    {
        var template = BuildTemplate(
            pageTitle: "Dates",
            fieldId: "startDate",
            fieldType: "date",
            labelValue: "Start date",
            labelVisible: true);

        var lookup = ResponseFieldMetadataResolver.BuildLookup(template);

        Assert.Equal("DateTime", ResponseFieldMetadataResolver.ResolveDataType("startDate", "not-a-date", lookup));
    }

    [Fact]
    public void ResolveDataType_UsesTemplateType_ForText()
    {
        var template = BuildTemplate(
            pageTitle: "Details",
            fieldId: "notes",
            fieldType: "text-area",
            labelValue: "Notes",
            labelVisible: true);

        var lookup = ResponseFieldMetadataResolver.BuildLookup(template);

        Assert.Equal("string", ResponseFieldMetadataResolver.ResolveDataType("notes", "2024-01-01", lookup));
    }

    [Fact]
    public void ResolveDataType_FallsBackToRuntimeValue_WhenFieldNotInTemplate()
    {
        var lookup = ResponseFieldMetadataResolver.BuildLookup(null);

        Assert.Equal("DateTime", ResponseFieldMetadataResolver.ResolveDataType("unknown", "2024-01-15", lookup));
        Assert.Equal("string", ResponseFieldMetadataResolver.ResolveDataType("unknown", "hello", lookup));
        Assert.Equal("number", ResponseFieldMetadataResolver.ResolveDataType("unknown", "42.5", lookup));
        Assert.Equal("boolean", ResponseFieldMetadataResolver.ResolveDataType("unknown", "true", lookup));
    }

    [Fact]
    public void BuildFormFieldEntry_IncludesQuestionValueCompletedAndDataType()
    {
        var template = BuildTemplate(
            pageTitle: "Benefits",
            fieldId: "reasonAndBenefitsAcademiesStrategicNeeds",
            fieldType: "text",
            labelValue: "Strategic needs",
            labelVisible: true);

        var lookup = ResponseFieldMetadataResolver.BuildLookup(template);
        var entry = ResponseFieldMetadataResolver.BuildFormFieldEntry(
            "reasonAndBenefitsAcademiesStrategicNeeds",
            "asdasd",
            lookup);

        var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        using var doc = JsonDocument.Parse(json);
        var field = doc.RootElement;

        Assert.Equal("Strategic needs", field.GetProperty("question").GetString());
        Assert.Equal("asdasd", field.GetProperty("value").GetString());
        Assert.True(field.GetProperty("completed").GetBoolean());
        Assert.Equal("string", field.GetProperty("dataType").GetString());
    }

    private static FormTemplate BuildTemplate(
        string pageTitle,
        string fieldId,
        string fieldType,
        string labelValue,
        bool labelVisible)
    {
        return new FormTemplate
        {
            TemplateId = "template-1",
            TemplateName = "Test",
            Description = "Test",
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
                            TaskId = "t1",
                            TaskName = "Task",
                            TaskOrder = 1,
                            TaskStatusString = "NotStarted",
                            Pages =
                            [
                                new Page
                                {
                                    PageId = "p1",
                                    Slug = "page-1",
                                    Title = pageTitle,
                                    Description = "desc",
                                    PageOrder = 1,
                                    Fields =
                                    [
                                        new Field
                                        {
                                            FieldId = fieldId,
                                            Type = fieldType,
                                            Order = 1,
                                            Label = new Label
                                            {
                                                Value = labelValue,
                                                IsVisible = labelVisible
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
}
