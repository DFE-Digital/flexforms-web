using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Application.Tests.FormEngine;

public class UploadFieldFileLimitTests
{
    private readonly IComplexFieldConfigurationService _configurations = Substitute.For<IComplexFieldConfigurationService>();

    public UploadFieldFileLimitTests()
    {
        _configurations.GetConfiguration("EvidenceUpload")
            .Returns(new ComplexFieldConfiguration { Id = "EvidenceUpload", FieldType = "upload", MaxSelections = 3 });
    }

    [Fact]
    public void Resolve_ShouldReturnTheConfiguredLimit_ForAFieldOnATaskPage()
    {
        var template = Template(TaskWith(pages: [PageWith(UploadField("evidence"))]));

        Assert.Equal(3, UploadFieldFileLimit.Resolve(template, "evidence", _configurations));
    }

    [Fact]
    public void Resolve_ShouldFindTheField_OnACollectionFlowPage()
    {
        var task = TaskWith(pages: null);
        task.Summary = new TaskSummaryConfiguration
        {
            Flows = [new MultiCollectionFlowConfiguration { FlowId = "items", Pages = [PageWith(UploadField("evidence"))] }]
        };

        Assert.Equal(3, UploadFieldFileLimit.Resolve(Template(task), "evidence", _configurations));
    }

    [Fact]
    public void Resolve_ShouldFindTheField_OnADerivedFlowPage()
    {
        var task = TaskWith(pages: null);
        task.Summary = new TaskSummaryConfiguration
        {
            Flows = null,
            DerivedFlows = [new DerivedCollectionFlowConfiguration { FlowId = "signatures", Pages = [PageWith(UploadField("evidence"))] }]
        };

        Assert.Equal(3, UploadFieldFileLimit.Resolve(Template(task), "evidence", _configurations));
    }

    [Fact]
    public void Resolve_ShouldSkipFlowsWithoutPages()
    {
        var task = TaskWith(pages: null);
        task.Summary = new TaskSummaryConfiguration
        {
            Flows = [new MultiCollectionFlowConfiguration { FlowId = "items", Pages = null! }],
            DerivedFlows = [new DerivedCollectionFlowConfiguration { FlowId = "signatures", Pages = null! }]
        };

        Assert.Equal(0, UploadFieldFileLimit.Resolve(Template(task), "evidence", _configurations));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_ShouldReturnZero_WhenThereIsNoFieldId(string? fieldId)
    {
        var template = Template(TaskWith(pages: [PageWith(UploadField("evidence"))]));

        Assert.Equal(0, UploadFieldFileLimit.Resolve(template, fieldId!, _configurations));
    }

    [Fact]
    public void Resolve_ShouldReturnZero_WhenThereIsNoTemplate()
    {
        Assert.Equal(0, UploadFieldFileLimit.Resolve(null, "evidence", _configurations));
    }

    [Fact]
    public void Resolve_ShouldReturnZero_WhenTheTemplateHasNoTaskGroups()
    {
        var template = Template(TaskWith(pages: null));
        template.TaskGroups = null!;

        Assert.Equal(0, UploadFieldFileLimit.Resolve(template, "evidence", _configurations));
    }

    [Fact]
    public void Resolve_ShouldReturnZero_WhenGroupsTasksAndPagesAreEmpty()
    {
        var template = Template(TaskWith(pages: [new Page { PageId = "p", Slug = "p", Title = "P", Description = "", PageOrder = 1, Fields = null! }]));
        template.TaskGroups.Add(new TaskGroup { GroupId = "g2", GroupName = "G2", GroupOrder = 2, GroupStatus = "NotStarted", Tasks = null! });

        Assert.Equal(0, UploadFieldFileLimit.Resolve(template, "evidence", _configurations));
    }

    [Fact]
    public void Resolve_ShouldReturnZero_WhenTheFieldIsNotAComplexField()
    {
        var field = UploadField("evidence");
        field.ComplexField = null;

        Assert.Equal(0, UploadFieldFileLimit.Resolve(Template(TaskWith(pages: [PageWith(field)])), "evidence", _configurations));
    }

    [Fact]
    public void Resolve_ShouldReturnZero_WhenTheComplexFieldHasNoId()
    {
        var field = UploadField("evidence");
        field.ComplexField = new ComplexField { Id = " " };

        Assert.Equal(0, UploadFieldFileLimit.Resolve(Template(TaskWith(pages: [PageWith(field)])), "evidence", _configurations));
    }

    [Fact]
    public void Resolve_ShouldReturnZero_WhenTheConfigurationIsMissing()
    {
        var field = UploadField("evidence");
        field.ComplexField = new ComplexField { Id = "UnknownUpload" };

        Assert.Equal(0, UploadFieldFileLimit.Resolve(Template(TaskWith(pages: [PageWith(field)])), "evidence", _configurations));
    }

    [Fact]
    public void Resolve_ShouldTreatANegativeLimitAsNoLimit()
    {
        _configurations.GetConfiguration("EvidenceUpload")
            .Returns(new ComplexFieldConfiguration { Id = "EvidenceUpload", FieldType = "upload", MaxSelections = -1 });

        var template = Template(TaskWith(pages: [PageWith(UploadField("evidence"))]));

        Assert.Equal(0, UploadFieldFileLimit.Resolve(template, "evidence", _configurations));
    }

    private static FormTemplate Template(Domain.Models.Task task) =>
        new()
        {
            TemplateId = "tpl",
            TemplateName = "Template",
            Description = string.Empty,
            TaskGroups =
            [
                new TaskGroup { GroupId = "g1", GroupName = "Group", GroupOrder = 1, GroupStatus = "NotStarted", Tasks = [task] }
            ]
        };

    private static Domain.Models.Task TaskWith(List<Page>? pages) =>
        new() { TaskId = "t1", TaskName = "Task", TaskOrder = 1, TaskStatusString = "NotStarted", Pages = pages };

    private static Page PageWith(Field field) =>
        new() { PageId = "p1", Slug = "p1", Title = "Page", Description = string.Empty, PageOrder = 1, Fields = [field] };

    private static Field UploadField(string fieldId) =>
        new()
        {
            FieldId = fieldId,
            Type = "complexField",
            Label = new Label { Value = "Evidence" },
            Order = 1,
            ComplexField = new ComplexField { Id = "EvidenceUpload" }
        };
}
