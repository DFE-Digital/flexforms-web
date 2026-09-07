using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using TaskModel = GovUK.Dfe.FlexForms.Domain.Models.Task;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Services;

public class FieldRequirementServiceTests
{
    private readonly FieldRequirementService _service = new(NullLogger<FieldRequirementService>.Instance);

    [Fact]
    public void IsFieldRequired_prefers_validation_rule_then_flag_then_template_policy()
    {
        var template = Template("optional");
        var byRule = Field("a", required: false, validations: [Required("Enter A")]);
        var byFlag = Field("b", required: true);
        var byPolicy = Field("c", required: null);

        Assert.True(_service.IsFieldRequired(byRule, template));
        Assert.True(_service.IsFieldRequired(byFlag, template));
        Assert.False(_service.IsFieldRequired(byPolicy, template));
        Assert.True(_service.IsFieldRequired(byPolicy, Template("required")));
    }

    [Fact]
    public void GetMissingRequiredFieldsWithMessages_skips_hidden_fields_and_uses_custom_messages()
    {
        var field = Field("name", required: true, validations: [Required("Enter the name")]);
        var hidden = Field("secret", required: true);
        var task = new TaskModel
        {
            TaskId = "t1",
            TaskName = "About you",
            TaskOrder = 1,
            TaskStatusString = "NotStarted",
            Pages = [Page("p1", [field, hidden])]
        };

        var missing = _service.GetMissingRequiredFieldsWithMessages(
            task,
            Template("optional"),
            new Dictionary<string, object>(),
            fieldId => fieldId == "secret");

        Assert.Equal("Enter the name", missing["name"]);
        Assert.DoesNotContain("secret", missing.Keys);
    }

    [Fact]
    public void GetMissingRequiredFieldsWithMessages_returns_empty_when_values_are_present()
    {
        var field = Field("name", required: true);
        var task = new TaskModel
        {
            TaskId = "t1",
            TaskName = "About you",
            TaskOrder = 1,
            TaskStatusString = "NotStarted",
            Pages = [Page("p1", [field])]
        };

        var missing = _service.GetMissingRequiredFieldsWithMessages(
            task,
            Template("optional"),
            new Dictionary<string, object> { ["name"] = "Ada" });

        Assert.Empty(missing);
    }

    [Fact]
    public void GetRequiredFieldsForTask_skips_hidden_fields_and_null_pages()
    {
        var visible = Field("visible", required: true);
        var hidden = Field("hidden", required: true);
        var task = new TaskModel
        {
            TaskId = "t1",
            TaskName = "About you",
            TaskOrder = 1,
            TaskStatusString = "NotStarted",
            Pages = [Page("p1", [visible, hidden])]
        };

        var required = _service.GetRequiredFieldsForTask(
            task,
            Template("optional"),
            fieldId => fieldId == "hidden");

        Assert.Equal(["visible"], required);
        Assert.Empty(_service.GetRequiredFieldsForTask(
            new TaskModel
            {
                TaskId = "t1",
                TaskName = "Empty",
                TaskOrder = 1,
                TaskStatusString = "NotStarted",
                Pages = null
            },
            Template("optional")));
    }

    [Fact]
    public void GetMissingRequiredFields_treats_empty_upload_array_as_missing()
    {
        var upload = Field("files", required: true);
        upload.Type = "upload";
        var task = new TaskModel
        {
            TaskId = "t1",
            TaskName = "Uploads",
            TaskOrder = 1,
            TaskStatusString = "NotStarted",
            Pages = [Page("p1", [upload])]
        };

        var missing = _service.GetMissingRequiredFields(
            task,
            Template("optional"),
            new Dictionary<string, object> { ["files"] = "[]" });

        Assert.Equal(["files"], missing);
    }

    [Fact]
    public void GetMissingRequiredFieldsWithMessages_uses_default_label_when_no_custom_message()
    {
        var field = Field("email", required: true);
        field.Label = new Label { Value = "Email address" };
        var task = new TaskModel
        {
            TaskId = "t1",
            TaskName = "Contact",
            TaskOrder = 1,
            TaskStatusString = "NotStarted",
            Pages = [Page("p1", [field])]
        };

        var missing = _service.GetMissingRequiredFieldsWithMessages(
            task,
            Template("optional"),
            new Dictionary<string, object>());

        Assert.Equal("• Email address", missing["email"]);
    }

    [Fact]
    public void IsFieldRequired_returns_false_when_explicitly_optional()
    {
        var field = Field("notes", required: false);

        Assert.False(_service.IsFieldRequired(field, Template("required")));
    }

    [Fact]
    public void GetMissingRequiredFieldsWithMessages_returns_empty_when_task_has_no_pages()
    {
        var missing = _service.GetMissingRequiredFieldsWithMessages(
            new TaskModel
            {
                TaskId = "t1",
                TaskName = "Empty",
                TaskOrder = 1,
                TaskStatusString = "NotStarted",
                Pages = null
            },
            Template("optional"),
            new Dictionary<string, object>());

        Assert.Empty(missing);
    }

    private static FormTemplate Template(string policy) =>
        new()
        {
            TemplateId = "tpl",
            TemplateName = "tpl",
            Description = "tpl",
            DefaultFieldRequirementPolicy = policy,
            TaskGroups = []
        };

    private static Field Field(string id, bool? required, List<ValidationRule>? validations = null) =>
        new()
        {
            FieldId = id,
            Type = "text",
            Label = new Label { Value = id },
            Order = 1,
            Required = required,
            Validations = validations
        };

    private static ValidationRule Required(string message) =>
        new() { Type = "required", Rule = "true", Message = message };

    private static Page Page(string id, List<Field> fields) =>
        new()
        {
            PageId = id,
            Slug = id,
            Title = id,
            Description = id,
            PageOrder = 1,
            Fields = fields
        };
}
