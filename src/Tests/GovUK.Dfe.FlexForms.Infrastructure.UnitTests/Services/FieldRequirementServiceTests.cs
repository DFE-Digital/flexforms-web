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
