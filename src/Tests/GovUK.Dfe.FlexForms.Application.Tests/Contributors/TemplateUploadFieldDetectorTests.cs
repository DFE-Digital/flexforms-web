using GovUK.Dfe.FlexForms.Application.Contributors;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Application.Tests.Contributors;

public class TemplateUploadFieldDetectorTests
{
    [Fact]
    public void HasUploadFields_IsFalse_WhenTemplateHasNoUploads()
    {
        var complexFields = Substitute.For<IComplexFieldConfigurationService>();
        complexFields.GetConfiguration(Arg.Any<string>())
            .Returns(new ComplexFieldConfiguration { Id = "search", FieldType = "autocomplete" });

        var template = TemplateWithField(new Field
        {
            FieldId = "name",
            Type = "text",
            Order = 1,
            Label = new Label { Value = "Name" }
        });

        Assert.False(TemplateUploadFieldDetector.HasUploadFields(template, complexFields));
    }

    [Fact]
    public void HasUploadFields_IsTrue_WhenFieldTypeIsUpload()
    {
        var complexFields = Substitute.For<IComplexFieldConfigurationService>();
        var template = TemplateWithField(new Field
        {
            FieldId = "evidence",
            Type = "upload",
            Order = 1,
            Label = new Label { Value = "Evidence" }
        });

        Assert.True(TemplateUploadFieldDetector.HasUploadFields(template, complexFields));
    }

    [Fact]
    public void HasUploadFields_IsTrue_WhenComplexFieldIsConfiguredAsUpload()
    {
        var complexFields = Substitute.For<IComplexFieldConfigurationService>();
        complexFields.GetConfiguration("EvidenceUpload")
            .Returns(new ComplexFieldConfiguration { Id = "EvidenceUpload", FieldType = "upload" });

        var template = TemplateWithField(new Field
        {
            FieldId = "evidence",
            Type = "complexField",
            Order = 1,
            Label = new Label { Value = "Evidence" },
            ComplexField = new ComplexField { Id = "EvidenceUpload" }
        });

        Assert.True(TemplateUploadFieldDetector.HasUploadFields(template, complexFields));
    }

    [Fact]
    public void HasUploadFields_IsTrue_WhenUploadIsOnlyOnMultiCollectionFlowPage()
    {
        var complexFields = Substitute.For<IComplexFieldConfigurationService>();
        var template = TemplateWithTask(new Domain.Models.Task
        {
            TaskId = "t1",
            TaskName = "Task",
            TaskOrder = 1,
            TaskStatusString = "NotStarted",
            Pages = [],
            Summary = new TaskSummaryConfiguration
            {
                Mode = "multiCollectionFlow",
                Flows =
                [
                    new MultiCollectionFlowConfiguration
                    {
                        FlowId = "flow1",
                        Title = "Items",
                        FieldId = "items",
                        Pages =
                        [
                            new Page
                            {
                                PageId = "flow-page",
                                Slug = "item",
                                Title = "Item",
                                Description = "Item",
                                PageOrder = 1,
                                Fields =
                                [
                                    new Field
                                    {
                                        FieldId = "evidence",
                                        Type = "upload",
                                        Order = 1,
                                        Label = new Label { Value = "Evidence" }
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }
        });

        Assert.True(TemplateUploadFieldDetector.HasUploadFields(template, complexFields));
    }

    [Fact]
    public void HasUploadFields_IsTrue_WhenUploadIsOnlyOnDerivedCollectionFlowPage()
    {
        var complexFields = Substitute.For<IComplexFieldConfigurationService>();
        var template = TemplateWithTask(new Domain.Models.Task
        {
            TaskId = "t1",
            TaskName = "Task",
            TaskOrder = 1,
            TaskStatusString = "NotStarted",
            Pages = [],
            Summary = new TaskSummaryConfiguration
            {
                Mode = "derivedCollectionFlow",
                DerivedFlows =
                [
                    new DerivedCollectionFlowConfiguration
                    {
                        FlowId = "derived1",
                        Title = "Derived",
                        SourceFieldId = "source",
                        FieldId = "derived",
                        Pages =
                        [
                            new Page
                            {
                                PageId = "derived-page",
                                Slug = "derived",
                                Title = "Derived",
                                Description = "Derived",
                                PageOrder = 1,
                                Fields =
                                [
                                    new Field
                                    {
                                        FieldId = "evidence",
                                        Type = "upload",
                                        Order = 1,
                                        Label = new Label { Value = "Evidence" }
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }
        });

        Assert.True(TemplateUploadFieldDetector.HasUploadFields(template, complexFields));
    }

    private static FormTemplate TemplateWithField(Field field) => TemplateWithTask(new Domain.Models.Task
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
                Slug = "page",
                Title = "Page",
                Description = "Page",
                PageOrder = 1,
                Fields = [field]
            }
        ]
    });

    private static FormTemplate TemplateWithTask(Domain.Models.Task task) => new()
    {
        TemplateId = Guid.NewGuid().ToString(),
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
                Tasks = [task]
            }
        ]
    };
}
