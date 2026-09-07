using AutoFixture;
using GovUK.Dfe.CoreLibs.Testing.AutoFixture.Customizations;
using GovUK.Dfe.CoreLibs.Testing.Helpers;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Services;

public class FormValidationOrchestratorTests
{
    private readonly IFixture _fixture;
    private readonly FormValidationOrchestrator _orchestrator;

    public FormValidationOrchestratorTests()
    {
        _fixture = FixtureFactoryHelper.ConfigureFixtureFactory([
            typeof(NSubstituteWithMembersCustomization),
            typeof(OmitCircularReferenceCustomization)
        ]);
        
        _fixture.Customize<Condition>(ob => ob.Without(rule => rule.Conditions));

        var fieldRequirementService = Substitute.For<IFieldRequirementService>();
        fieldRequirementService.IsFieldRequired(Arg.Any<Field>(), Arg.Any<FormTemplate>()).Returns(false);
        _fixture.Register(() => fieldRequirementService);
        
        _orchestrator = _fixture.Create<FormValidationOrchestrator>();
    }

    [Theory]
    [InlineData("radios", "I <em>haven't</em> eaten the cookie", "I &lt;em&gt;haven&#39;t&lt;/em&gt; eaten the cookie")]
    [InlineData("checkboxes", "I have eaten the cookie", "I have eaten the cookie")]
    public void ValidateField_when_required_field_with_options_and_submittedValue_is_in_options_then_returns_true(string fieldType, string optionValue, string submittedValue)
    {
        var option1 = _fixture.Build<Option>()
            .With(o => o.Value, optionValue)
            .Create();
        var option2 = _fixture.Build<Option>()
            .With(o => o.Value, "something-else")
            .Create();
        var validation = _fixture.Build<ValidationRule>()
            .With(v => v.Type, "required")
            .Without(v => v.Condition)
            .With(v => v.Message, "This field is required")
            .Create();
        var field = _fixture.Build<Field>()
            .With(f => f.Type, fieldType)
            .With(f => f.Options, [option1, option2])
            .With(f => f.Validations, [validation])
            .Create();
        
        var formData = _fixture.Create<Dictionary<string, object>?>();
        var fieldKey = field.FieldId;
        var formTemplate = _fixture.Create<FormTemplate>();

        var result = _orchestrator.ValidateField(field, submittedValue, formData, fieldKey, formTemplate);
        
        Assert.True(result.IsValid);
    }
    
    [Theory]
    [InlineData("radios", "not-an-option")]
    [InlineData("checkboxes", "not-an-option")]
    public void ValidateField_when_required_field_with_options_and_submittedValue_is_not_in_options_then_returns_false_and_uses_required_message(string fieldType, string submittedValue)
    {
        var option1 = _fixture.Build<Option>()
            .With(o => o.Value, "option1")
            .Create();
        var option2 = _fixture.Build<Option>()
            .With(o => o.Value, "option2")
            .Create();
        var validation = _fixture.Build<ValidationRule>()
            .With(v => v.Type, "required")
            .Without(v => v.Condition)
            .With(v => v.Message, "This field is required")
            .Create();
        var field = _fixture.Build<Field>()
            .With(f => f.Type, fieldType)
            .With(f => f.Options, [option1, option2])
            .With(f => f.Validations, [validation])
            .Create();
        
        var formData = _fixture.Create<Dictionary<string, object>?>();
        var fieldKey = field.FieldId;
        var formTemplate = _fixture.Create<FormTemplate>();

        var result = _orchestrator.ValidateField(field, submittedValue, formData, fieldKey, formTemplate);
        
        Assert.False(result.IsValid);
        Assert.Equal("This field is required", result.Errors[0].Message);
    }
    
    [Theory]
    [InlineData("radios")]
    [InlineData("checkboxes")]
    public void ValidateField_when_optional_field_with_options_and_submittedValue_is_nonempty_and_not_in_options_then_returns_false_and_uses_fallback_message(string fieldType)
    {
        var option1 = _fixture.Build<Option>()
            .With(o => o.Value, "option1")
            .Create();
        var option2 = _fixture.Build<Option>()
            .With(o => o.Value, "option2")
            .Create();
        var field = _fixture.Build<Field>()
            .With(f => f.Type, fieldType)
            .With(f => f.Options, [option1, option2])
            .Without(f => f.Validations)
            .Create();
        
        var formData = _fixture.Create<Dictionary<string, object>?>();
        var fieldKey = field.FieldId;
        var formTemplate = _fixture.Create<FormTemplate>();

        var result = _orchestrator.ValidateField(field, "not-an-option", formData, fieldKey, formTemplate);
        
        Assert.False(result.IsValid);
        Assert.Equal("Select an option from the list", result.Errors[0].Message);
    }
    
    [Theory]
    [InlineData("radios", "")]
    [InlineData("radios", "    ")]
    [InlineData("checkboxes", "")]
    [InlineData("checkboxes", "    ")]
    public void ValidateField_when_optional_field_with_options_and_submittedValue_is_empty_then_returns_true(string fieldType, string submittedValue)
    {
        var option1 = _fixture.Build<Option>()
            .With(o => o.Value, "option1")
            .Create();
        var option2 = _fixture.Build<Option>()
            .With(o => o.Value, "option2")
            .Create();
        var field = _fixture.Build<Field>()
            .With(f => f.Type, fieldType)
            .With(f => f.Options, [option1, option2])
            .Without(f => f.Validations)
            .Create();
        
        var formData = _fixture.Create<Dictionary<string, object>?>();
        var fieldKey = field.FieldId;
        var formTemplate = _fixture.Create<FormTemplate>();

        var result = _orchestrator.ValidateField(field, submittedValue, formData, fieldKey, formTemplate);
        
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateField_when_maxLength_and_submitted_value_contains_html_entity_then_uses_decoded_length()
    {
        var maxLengthRule = _fixture.Build<ValidationRule>()
            .With(v => v.Type, "maxLength")
            .Without(v => v.Condition)
            .With(v => v.Rule, "5")
            .With(v => v.Message, "Too many characters")
            .Create();
        var field = _fixture.Build<Field>()
            .With(f => f.Type, "character-count")
            .With(f => f.Validations, [maxLengthRule])
            .Create();

        var formData = _fixture.Create<Dictionary<string, object>?>();
        var fieldKey = field.FieldId;
        var formTemplate = _fixture.Create<FormTemplate>();

        // User sees five characters (EMAT + U+2019); submitted value may arrive as an HTML numeric character reference.
        const string submittedEncoded = "EMAT&#x2019;";

        var result = _orchestrator.ValidateField(field, submittedEncoded, formData, fieldKey, formTemplate);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateField_when_maxLength_and_decoded_length_exceeds_limit_then_returns_false()
    {
        var maxLengthRule = _fixture.Build<ValidationRule>()
            .With(v => v.Type, "maxLength")
            .Without(v => v.Condition)
            .With(v => v.Rule, "4")
            .With(v => v.Message, "Too many characters")
            .Create();
        var field = _fixture.Build<Field>()
            .With(f => f.Type, "character-count")
            .With(f => f.Validations, [maxLengthRule])
            .Create();

        var formData = _fixture.Create<Dictionary<string, object>?>();
        var fieldKey = field.FieldId;
        var formTemplate = _fixture.Create<FormTemplate>();

        const string submittedEncoded = "EMAT&#x2019;";

        var result = _orchestrator.ValidateField(field, submittedEncoded, formData, fieldKey, formTemplate);

        Assert.False(result.IsValid);
        Assert.Equal("Too many characters", result.Errors[0].Message);
    }

    [Fact]
    public void ValidateField_when_maxLength_and_value_is_sanitised_with_br_tags_then_uses_plain_text_length()
    {
        // Same shape as after DisplayHelpers.SanitiseHtmlInput for "Some\r\nnew\rlines\nhere" (see DisplayHelpersTests).
        const string sanitisedAsStored = "Some<br>new<br>lines<br>here";
        const int plainTextLength = 19;

        var maxLengthRule = _fixture.Build<ValidationRule>()
            .With(v => v.Type, "maxLength")
            .Without(v => v.Condition)
            .With(v => v.Rule, plainTextLength.ToString())
            .With(v => v.Message, "Too many characters")
            .Create();
        var field = _fixture.Build<Field>()
            .With(f => f.Type, "character-count")
            .With(f => f.Validations, [maxLengthRule])
            .Create();

        var formData = _fixture.Create<Dictionary<string, object>?>();
        var fieldKey = field.FieldId;
        var formTemplate = _fixture.Create<FormTemplate>();

        var result = _orchestrator.ValidateField(field, sanitisedAsStored, formData, fieldKey, formTemplate);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateField_when_maxLength_and_sanitised_value_exceeds_plain_limit_then_returns_false()
    {
        const string sanitisedAsStored = "Some<br>new<br>lines<br>here";

        var maxLengthRule = _fixture.Build<ValidationRule>()
            .With(v => v.Type, "maxLength")
            .Without(v => v.Condition)
            .With(v => v.Rule, "18")
            .With(v => v.Message, "Too many characters")
            .Create();
        var field = _fixture.Build<Field>()
            .With(f => f.Type, "character-count")
            .With(f => f.Validations, [maxLengthRule])
            .Create();

        var formData = _fixture.Create<Dictionary<string, object>?>();
        var fieldKey = field.FieldId;
        var formTemplate = _fixture.Create<FormTemplate>();

        var result = _orchestrator.ValidateField(field, sanitisedAsStored, formData, fieldKey, formTemplate);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateField_when_maxWord_and_sanitised_value_exceeds_plain_limit_then_returns_false()
    {
        const string sanitisedAsStored = "Some<br>new<br>lines<br>here";
        bool result = ValidateFieldWordCount(sanitisedAsStored, 3);
        Assert.False(result);
    }

    [Fact]
    public void ValidateField_when_maxWord_and_sanitised_value_within_plain_limit_then_returns_true()
    {
        const string sanitisedAsStored = "Some<br>new<br>lines<br>here";
        bool result = ValidateFieldWordCount(sanitisedAsStored, 5);
        Assert.True(result);
    }

    [Fact]
    public void ValidateField_complex_field_when_maxWord_and_sanitised_value_exceeds_plain_limit_then_returns_false()
    {
        const string sanitisedAsStored = "Some<br>new<br>lines<br>here";
        bool result = ValidateComplexFieldWordCount(sanitisedAsStored, 3);
        Assert.False(result);
    }

    [Fact]
    public void ValidateField_complex_field_when_maxWord_and_sanitised_value_within_plain_limit_then_returns_true()
    {
        const string sanitisedAsStored = "Some<br>new<br>lines<br>here";
        bool result = ValidateComplexFieldWordCount(sanitisedAsStored, 5);
        Assert.True(result);
    }

    private bool ValidateFieldWordCount(string sanitisedText, short limit)
    {
        var rule = _fixture.Build<ValidationRule>()
            .With(v => v.Type, "maxWords")
            .Without(v => v.Condition)
            .With(v => v.Rule, limit)
            .With(v => v.Message, "Too many words")
            .Create();
        var field = _fixture.Build<Field>()
            .With(f => f.Type, "word-count")
            .With(f => f.Validations, [rule])
            .Create();

        var formData = _fixture.Create<Dictionary<string, object>?>();
        var fieldKey = field.FieldId;
        var formTemplate = _fixture.Create<FormTemplate>();

        return _orchestrator.ValidateField(field, sanitisedText, formData, fieldKey, formTemplate).IsValid;
    }

    private bool ValidateComplexFieldWordCount(string sanitisedText, short limit)
    {
        var rule = _fixture.Build<ValidationRule>()
            .With(v => v.Type, "maxWords")
            .Without(v => v.Condition)
            .With(v => v.Rule, limit)
            .With(v => v.Message, "Too many words")
            .Create();
        var field = _fixture.Build<Field>()
            .With(f => f.Type, "complexField")
            .With(f => f.Validations, [rule])
            .Create();

        var formData = _fixture.Create<Dictionary<string, object>?>();
        var fieldKey = field.FieldId;
        var formTemplate = _fixture.Create<FormTemplate>();

        return _orchestrator.ValidateField(field, sanitisedText, formData, fieldKey, formTemplate).IsValid;
    }

    [Fact]
    public void ValidatePage_when_required_text_field_is_empty_then_returns_error()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var page = CreatePage(("name", "text", [RequiredRule("Enter your name")]));

        var result = orchestrator.ValidatePage(page, []);

        Assert.False(result.IsValid);
        Assert.Equal("Enter your name", result.Errors[0].Message);
    }

    [Fact]
    public void ValidatePage_when_email_field_is_invalid_then_returns_error()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var page = CreatePage(("email", "email", []));

        var result = orchestrator.ValidatePage(page, new Dictionary<string, object> { ["email"] = "not-an-email" });

        Assert.False(result.IsValid);
        Assert.Contains("email address", result.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePage_when_date_field_has_missing_parts_then_returns_error()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var page = CreatePage(
            ("startDate", "date", []),
            label: new Label { Value = "Start date", ValidationLabelValue = "Start date" });

        var result = orchestrator.ValidatePage(page, new Dictionary<string, object> { ["startDate"] = "--" });

        Assert.False(result.IsValid);
        Assert.Contains("day, month and year", result.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePage_when_date_field_is_not_real_then_returns_error()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var page = CreatePage(
            ("startDate", "date", []),
            label: new Label { Value = "Start date", ValidationLabelValue = "Start date" });

        var result = orchestrator.ValidatePage(page, new Dictionary<string, object> { ["startDate"] = "2024-02-30" });

        Assert.False(result.IsValid);
        Assert.Contains("real date", result.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePage_when_number_field_is_required_and_empty_then_returns_error()
    {
        var fieldRequirementService = Substitute.For<IFieldRequirementService>();
        fieldRequirementService.IsFieldRequired(Arg.Any<Field>(), Arg.Any<FormTemplate>()).Returns(true);
        var orchestrator = CreateExplicitOrchestrator(fieldRequirementService);
        var template = new FormTemplate
        {
            TemplateId = "t1",
            TemplateName = "Template",
            Description = "Template",
            TaskGroups = [],
            DefaultFieldRequirementPolicy = "required"
        };
        var page = CreatePage(
            ("amount", "number", []),
            label: new Label { Value = "Amount" });

        var result = orchestrator.ValidatePage(page, [], template);

        Assert.False(result.IsValid);
        Assert.Contains("Amount is required", result.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidatePage_when_regex_rule_fails_then_returns_error()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var page = CreatePage(("ref", "text", [RegexRule("^[A-Z]{3}$", "Enter a 3-letter code")]));

        var result = orchestrator.ValidatePage(page, new Dictionary<string, object> { ["ref"] = "abc123" });

        Assert.False(result.IsValid);
        Assert.Equal("Enter a 3-letter code", result.Errors[0].Message);
    }

    [Fact]
    public void ValidatePage_when_conditional_required_rule_is_not_met_then_skips_validation()
    {
        var conditionalEngine = Substitute.For<IConditionalLogicEngine>();
        conditionalEngine
            .EvaluateCondition(Arg.Any<Condition>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<ConditionalLogicContext?>())
            .Returns(false);

        var orchestrator = CreateExplicitOrchestrator(conditionalLogicEngine: conditionalEngine);
        var conditionalRequired = new ValidationRule
        {
            Type = "required",
            Rule = string.Empty,
            Message = "Conditional field is required",
            Condition = new Condition
            {
                TriggerField = "showExtra",
                Operator = ConditionalLogicConstants.Operators.Equals,
                Value = "yes",
                DataType = ConditionalLogicConstants.DataTypes.String
            }
        };
        var page = CreatePage(("extra", "text", [conditionalRequired]));

        var result = orchestrator.ValidatePage(
            page,
            new Dictionary<string, object> { ["showExtra"] = "no", ["extra"] = "" });

        Assert.True(result.IsValid);
        conditionalEngine.Received(1).EvaluateCondition(
            Arg.Is<Condition>(c => c.TriggerField == "showExtra"),
            Arg.Any<Dictionary<string, object>>(),
            Arg.Any<ConditionalLogicContext?>());
    }

    [Fact]
    public void ValidatePage_when_conditional_required_rule_is_met_and_value_missing_then_returns_error()
    {
        var conditionalEngine = new ConditionalLogicEngine(NullLogger<ConditionalLogicEngine>.Instance);
        var orchestrator = CreateExplicitOrchestrator(conditionalLogicEngine: conditionalEngine);
        var conditionalRequired = new ValidationRule
        {
            Type = "required",
            Rule = string.Empty,
            Message = "Extra detail is required",
            Condition = new Condition
            {
                TriggerField = "showExtra",
                Operator = ConditionalLogicConstants.Operators.Equals,
                Value = "yes",
                DataType = ConditionalLogicConstants.DataTypes.String
            }
        };
        var page = CreatePage(("extra", "text", [conditionalRequired]));

        var result = orchestrator.ValidatePage(
            page,
            new Dictionary<string, object> { ["showExtra"] = "yes", ["extra"] = "" });

        Assert.False(result.IsValid);
        Assert.Equal("Extra detail is required", result.Errors[0].Message);
    }

    [Fact]
    public void ValidateTask_when_task_page_has_required_field_then_validates_all_pages()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var task = new Domain.Models.Task
        {
            TaskId = "task-1",
            TaskName = "Task 1",
            TaskOrder = 1,
            TaskStatusString = "NotStarted",
            Pages =
            [
                CreatePage(("field-a", "text", [RequiredRule("Field A is required")])),
                new Page
                {
                    PageId = "page-2",
                    Slug = "page-2",
                    Title = "Page 2",
                    Description = string.Empty,
                    PageOrder = 2,
                    Fields =
                    [
                        new Field
                        {
                            FieldId = "field-b",
                            Type = "text",
                            Label = new Label { Value = "Field B" },
                            Order = 1,
                            Validations = [RequiredRule("Field B is required")]
                        }
                    ]
                }
            ]
        };

        var result = orchestrator.ValidateTask(task, new Dictionary<string, object> { ["field-a"] = "done" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message == "Field B is required");
    }

    private static FormValidationOrchestrator CreateExplicitOrchestrator(
        IFieldRequirementService? fieldRequirementService = null,
        IConditionalLogicEngine? conditionalLogicEngine = null)
    {
        fieldRequirementService ??= Substitute.For<IFieldRequirementService>();
        conditionalLogicEngine ??= new ConditionalLogicEngine(NullLogger<ConditionalLogicEngine>.Instance);

        return new FormValidationOrchestrator(
            NullLogger<FormValidationOrchestrator>.Instance,
            conditionalLogicEngine,
            fieldRequirementService);
    }

    private static ValidationRule RequiredRule(string message) =>
        new()
        {
            Type = "required",
            Rule = string.Empty,
            Message = message
        };

    private static ValidationRule RegexRule(string pattern, string message) =>
        new()
        {
            Type = "regex",
            Rule = pattern,
            Message = message
        };

    private static Page CreatePage(
        (string FieldId, string Type, ValidationRule[] Validations) field,
        Label? label = null)
    {
        return new Page
        {
            PageId = "page-1",
            Slug = "page-1",
            Title = "Page 1",
            Description = string.Empty,
            PageOrder = 1,
            Fields =
            [
                new Field
                {
                    FieldId = field.FieldId,
                    Type = field.Type,
                    Label = label ?? new Label { Value = field.FieldId },
                    Order = 1,
                    Validations = field.Validations.ToList()
                }
            ]
        };
    }
}