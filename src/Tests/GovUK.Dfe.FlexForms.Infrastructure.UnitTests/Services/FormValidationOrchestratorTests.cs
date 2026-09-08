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

    [Fact]
    public void ValidateApplication_when_template_is_null_then_returns_success()
    {
        var orchestrator = CreateExplicitOrchestrator();

        var result = orchestrator.ValidateApplication(null!, []);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateApplication_when_template_has_no_task_groups_then_returns_success()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var template = CreateTemplate();
        template.TaskGroups = null!;

        var result = orchestrator.ValidateApplication(template, []);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateApplication_when_a_task_field_is_missing_then_returns_error_for_that_field()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var template = CreateTemplate(CreatePage(("name", "text", [RequiredRule("Enter your name")])));

        var result = orchestrator.ValidateApplication(template, []);

        Assert.False(result.IsValid);
        Assert.Equal("Enter your name", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void ValidateApplication_when_all_task_fields_are_supplied_then_returns_success()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var template = CreateTemplate(CreatePage(("name", "text", [RequiredRule("Enter your name")])));

        var result = orchestrator.ValidateApplication(template, new Dictionary<string, object> { ["name"] = "Alice" });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidatePage_when_page_is_null_then_returns_success()
    {
        var orchestrator = CreateExplicitOrchestrator();

        Assert.True(orchestrator.ValidatePage(null!, []).IsValid);
    }

    [Fact]
    public void ValidatePage_when_page_has_no_fields_then_returns_success()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var page = CreatePage(("name", "text", [RequiredRule("Enter your name")]));
        page.Fields = null!;

        Assert.True(orchestrator.ValidatePage(page, []).IsValid);
    }

    [Fact]
    public void ValidateTask_when_task_is_null_then_returns_success()
    {
        var orchestrator = CreateExplicitOrchestrator();

        Assert.True(orchestrator.ValidateTask(null!, []).IsValid);
    }

    [Fact]
    public void ValidateTask_when_task_has_no_pages_then_returns_success()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var task = new Domain.Models.Task
        {
            TaskId = "task-1",
            TaskName = "Task 1",
            TaskOrder = 1,
            TaskStatusString = "NotStarted",
            Pages = null
        };

        Assert.True(orchestrator.ValidateTask(task, []).IsValid);
    }

    [Fact]
    public void ValidatePage_when_checkboxes_value_is_a_string_collection_then_each_option_is_validated()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var page = CreatePage(("choices", "checkboxes", []));
        page.Fields[0].Options =
        [
            new Option { Value = "a", Label = "A" },
            new Option { Value = "b", Label = "B" }
        ];

        var result = orchestrator.ValidatePage(
            page,
            new Dictionary<string, object> { ["choices"] = new[] { "a", "not-an-option" } });

        Assert.False(result.IsValid);
        Assert.Equal("Select an option from the list", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void ValidateField_when_checkboxes_value_is_a_json_array_of_valid_options_then_returns_true()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateField("checkboxes");
        field.Options =
        [
            new Option { Value = "a", Label = "A" },
            new Option { Value = "b", Label = "B" }
        ];

        var result = orchestrator.ValidateField(field, """["a","b"]""", "choices");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateField_when_radios_field_has_no_options_then_returns_select_an_option_error()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateField("radios");

        var result = orchestrator.ValidateField(field, "anything", "choice");

        Assert.False(result.IsValid);
        Assert.Equal("Select an option from the list", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void ValidateField_with_field_key_overload_skips_conditional_rules_because_form_data_is_absent()
    {
        var conditionalEngine = Substitute.For<IConditionalLogicEngine>();
        var orchestrator = CreateExplicitOrchestrator(conditionalLogicEngine: conditionalEngine);
        var field = CreateField("text", [ConditionalRequiredRule("Conditionally required")]);

        var result = orchestrator.ValidateField(field, string.Empty, "name");

        Assert.True(result.IsValid);
        conditionalEngine.DidNotReceiveWithAnyArgs().EvaluateCondition(default!, default!, default);
    }

    [Fact]
    public void ValidateField_with_form_data_overload_applies_required_rule()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateField("text", [RequiredRule("Enter your name")]);

        var result = orchestrator.ValidateField(field, string.Empty, new Dictionary<string, object>(), "name");

        Assert.False(result.IsValid);
        Assert.Equal("Enter your name", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void ValidateField_when_condition_evaluation_throws_then_rule_is_skipped()
    {
        var conditionalEngine = Substitute.For<IConditionalLogicEngine>();
        conditionalEngine
            .EvaluateCondition(Arg.Any<Condition>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<ConditionalLogicContext?>())
            .Returns(_ => throw new InvalidOperationException("boom"));

        var orchestrator = CreateExplicitOrchestrator(conditionalLogicEngine: conditionalEngine);
        var field = CreateField("text", [ConditionalRequiredRule("Conditionally required")]);

        var result = orchestrator.ValidateField(field, string.Empty, new Dictionary<string, object>(), "name");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateField_when_date_is_a_real_date_then_returns_true()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateField("date");

        Assert.True(orchestrator.ValidateField(field, "2024-02-29", "startDate").IsValid);
    }

    [Fact]
    public void ValidateField_when_date_is_empty_then_returns_true()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateField("date");

        Assert.True(orchestrator.ValidateField(field, string.Empty, "startDate").IsValid);
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("2024-01")]
    [InlineData("2024-1-5")]
    public void ValidateField_when_date_parts_are_present_but_unparseable_then_returns_real_date_error(string value)
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateField("date");
        field.Label = new Label { Value = "Start date", ValidationLabelValue = "the start date" };

        var result = orchestrator.ValidateField(field, value, "startDate");

        Assert.False(result.IsValid);
        Assert.Equal("the start date must be a real date", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void ValidateField_when_date_is_missing_parts_and_has_no_validation_label_then_uses_the_display_label()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateField("date");
        field.Label = new Label { Value = "Start date" };

        var result = orchestrator.ValidateField(field, "2024--1", "startDate");

        Assert.False(result.IsValid);
        Assert.Equal("Start date must include a day, month and year", Assert.Single(result.Errors).Message);
    }

    [Theory]
    [InlineData("email")]
    [InlineData("Email")]
    public void ValidateField_when_email_is_well_formed_then_returns_true(string fieldType)
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateField(fieldType);

        Assert.True(orchestrator.ValidateField(field, "name@example.com", "email").IsValid);
    }

    [Fact]
    public void ValidateField_when_email_is_empty_then_returns_true()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateField("email");

        Assert.True(orchestrator.ValidateField(field, string.Empty, "email").IsValid);
    }

    [Fact]
    public void ValidateField_when_template_policy_requires_field_and_label_is_absent_then_uses_the_field_id()
    {
        var orchestrator = CreateRequiringOrchestrator();
        var field = CreateField("text");
        field.Label = null!;

        var result = orchestrator.ValidateField(field, string.Empty, null, "name", CreateTemplate());

        Assert.False(result.IsValid);
        Assert.Equal("field-1 is required", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void ValidateField_when_template_policy_requires_field_and_value_is_supplied_then_returns_true()
    {
        var orchestrator = CreateRequiringOrchestrator();
        var field = CreateField("text");

        var result = orchestrator.ValidateField(field, "Alice", null, "name", CreateTemplate());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateField_when_template_policy_requires_field_and_an_explicit_required_rule_exists_then_returns_one_error()
    {
        var orchestrator = CreateRequiringOrchestrator();
        var field = CreateField("text", [RequiredRule("Enter your name")]);

        var result = orchestrator.ValidateField(field, string.Empty, null, "name", CreateTemplate());

        Assert.False(result.IsValid);
        Assert.Equal("Enter your name", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void ValidateField_when_regex_rule_has_no_pattern_then_rule_is_skipped()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateField("text", [RegexRule(string.Empty, "Wrong format")]);

        Assert.True(orchestrator.ValidateField(field, "anything", "ref").IsValid);
    }

    [Fact]
    public void ValidateField_when_regex_rule_and_value_is_empty_then_rule_is_skipped()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateField("text", [RegexRule("^[A-Z]{3}$", "Wrong format")]);

        Assert.True(orchestrator.ValidateField(field, string.Empty, "ref").IsValid);
    }

    [Fact]
    public void ValidateField_when_regex_rule_matches_then_returns_true()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateField("text", [RegexRule("^[A-Z]{3}$", "Wrong format")]);

        Assert.True(orchestrator.ValidateField(field, "ABC", "ref").IsValid);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("")]
    public void ValidateField_when_maxLength_rule_is_not_numeric_then_rule_is_skipped(string ruleValue)
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateField("character-count", [Rule("maxLength", ruleValue, "Too many characters")]);

        Assert.True(orchestrator.ValidateField(field, "a very long value indeed", "notes").IsValid);
    }

    [Fact]
    public void ValidateField_when_maxWords_rule_is_not_numeric_then_rule_is_skipped()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateField("word-count", [Rule("maxWords", "not-a-number", "Too many words")]);

        Assert.True(orchestrator.ValidateField(field, "one two three four five", "notes").IsValid);
    }

    [Fact]
    public void ValidateField_when_rule_type_is_unknown_then_no_error_is_added()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateField("text", [Rule("somethingUnsupported", "1", "Should never be shown")]);

        var result = orchestrator.ValidateField(field, "value", "name");

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateField_when_type_is_complexField_but_configuration_is_absent_then_standard_rules_apply()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateField("complexField", [RequiredRule("Choose an organisation")]);

        var result = orchestrator.ValidateField(field, string.Empty, "org");

        Assert.False(result.IsValid);
        Assert.Equal("Choose an organisation", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void ValidateComplexField_when_upload_field_is_required_by_template_policy_and_has_no_files_then_returns_error()
    {
        var orchestrator = CreateRequiringOrchestrator();
        var field = CreateComplexField("documentUpload");
        field.Label = new Label { Value = "Supporting documents" };

        var result = orchestrator.ValidateField(field, string.Empty, null, "docs", CreateTemplate());

        Assert.False(result.IsValid);
        Assert.Equal("Supporting documents is required", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void ValidateComplexField_when_upload_field_is_required_by_template_policy_and_has_files_then_returns_true()
    {
        var orchestrator = CreateRequiringOrchestrator();
        var field = CreateComplexField("documentUpload");

        var result = orchestrator.ValidateField(field, """[{"fileName":"a.pdf"}]""", null, "docs", CreateTemplate());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateComplexField_when_autocomplete_field_is_required_by_template_policy_and_is_empty_then_returns_error()
    {
        var orchestrator = CreateRequiringOrchestrator();
        var field = CreateComplexField("autocomplete");
        field.Label = null!;

        var result = orchestrator.ValidateField(field, "   ", null, "org", CreateTemplate());

        Assert.False(result.IsValid);
        Assert.Equal("field-1 is required", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void ValidateComplexField_when_there_are_no_validation_rules_and_no_template_then_returns_true()
    {
        var orchestrator = CreateRequiringOrchestrator();
        var field = CreateComplexField("autocomplete");

        Assert.True(orchestrator.ValidateField(field, string.Empty, "org").IsValid);
    }

    [Theory]
    [InlineData("Enter your phone number")]
    [InlineData("Enter your name")]
    [InlineData("Enter some text")]
    public void ValidateComplexField_when_upload_required_message_describes_a_text_field_then_an_upload_message_is_used(string templateMessage)
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateComplexField("documentUpload", [RequiredRule(templateMessage)]);

        var result = orchestrator.ValidateField(field, string.Empty, "docs");

        Assert.False(result.IsValid);
        Assert.Equal("Please upload a file.", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void ValidateComplexField_when_upload_required_message_is_already_upload_specific_then_it_is_used_unchanged()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateComplexField("documentUpload", [RequiredRule("Select a supporting document")]);

        var result = orchestrator.ValidateField(field, string.Empty, "docs");

        Assert.False(result.IsValid);
        Assert.Equal("Select a supporting document", Assert.Single(result.Errors).Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("UPLOAD_FIELD_SESSION_DATA")]
    [InlineData("[]")]
    public void ValidateComplexField_when_upload_value_indicates_no_files_then_the_required_rule_fails(string value)
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateComplexField("documentUpload", [RequiredRule("Select a supporting document")]);

        Assert.False(orchestrator.ValidateField(field, value, "docs").IsValid);
    }

    [Theory]
    [InlineData("""[{"fileName":"a.pdf"}]""")]
    [InlineData("[malformed")]
    [InlineData("[not-json]")]
    [InlineData("a.pdf")]
    public void ValidateComplexField_when_upload_value_indicates_files_then_the_required_rule_passes(string value)
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateComplexField("documentUpload", [RequiredRule("Select a supporting document")]);

        Assert.True(orchestrator.ValidateField(field, value, "docs").IsValid);
    }

    [Fact]
    public void ValidateComplexField_when_autocomplete_required_rule_and_value_is_empty_then_uses_the_rule_message()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateComplexField("autocomplete", [RequiredRule("Choose an organisation")]);

        var result = orchestrator.ValidateField(field, "   ", "org");

        Assert.False(result.IsValid);
        Assert.Equal("Choose an organisation", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void ValidateComplexField_when_autocomplete_required_rule_and_value_is_supplied_then_returns_true()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateComplexField("autocomplete", [RequiredRule("Choose an organisation")]);

        Assert.True(orchestrator.ValidateField(field, """{"name":"Trust A"}""", "org").IsValid);
    }

    [Theory]
    [InlineData("regex")]
    [InlineData("maxLength")]
    [InlineData("maxWords")]
    public void ValidateComplexField_when_non_required_rules_target_an_upload_field_then_they_are_skipped(string ruleType)
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateComplexField("documentUpload", [Rule(ruleType, "1", "Should never be shown")]);

        var result = orchestrator.ValidateField(field, "a very long list of many uploaded words", "docs");

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("""{"name":"Trust A"}""", "^Trust A$", true)]
    [InlineData("""{"name":"   "}""", "^Trust A$", false)]
    [InlineData("""{"ukprn":"12345678"}""", @"^\d{8}$", true)]
    [InlineData("""{"urn":123456}""", @"^\d{6}$", true)]
    [InlineData("""{"code":"AB1"}""", "^AB1$", true)]
    [InlineData("""{"id":true}""", @"^\{""id"":true\}$", true)]
    [InlineData("""{"other":"x"}""", @"^\{""other"":""x""\}$", true)]
    [InlineData("""["Trust A"]""", @"^\[""Trust A""\]$", true)]
    [InlineData("not-json", "^not-json$", true)]
    public void ValidateComplexField_when_regex_rule_applies_then_the_autocomplete_display_text_is_matched(
        string value,
        string pattern,
        bool expectedValid)
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateComplexField("autocomplete", [RegexRule(pattern, "Wrong format")]);

        var result = orchestrator.ValidateField(field, value, "org");

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void ValidateComplexField_when_regex_rule_has_no_pattern_then_rule_is_skipped()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateComplexField("autocomplete", [RegexRule(string.Empty, "Wrong format")]);

        Assert.True(orchestrator.ValidateField(field, "anything", "org").IsValid);
    }

    [Fact]
    public void ValidateComplexField_when_regex_rule_and_value_is_empty_then_rule_is_skipped()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateComplexField("autocomplete", [RegexRule("^[A-Z]{3}$", "Wrong format")]);

        Assert.True(orchestrator.ValidateField(field, string.Empty, "org").IsValid);
    }

    [Fact]
    public void ValidateComplexField_when_maxLength_rule_is_exceeded_then_returns_error()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateComplexField("autocomplete", [Rule("maxLength", "3", "Too many characters")]);

        var result = orchestrator.ValidateField(field, "abcd", "org");

        Assert.False(result.IsValid);
        Assert.Equal("Too many characters", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void ValidateComplexField_when_maxLength_rule_is_not_numeric_then_rule_is_skipped()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateComplexField("autocomplete", [Rule("maxLength", "not-a-number", "Too many characters")]);

        var result = orchestrator.ValidateField(field, "a very long value indeed", "org");

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateComplexField_when_rule_type_is_unknown_then_no_error_is_added()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateComplexField("autocomplete", [Rule("somethingUnsupported", "1", "Should never be shown")]);

        var result = orchestrator.ValidateField(field, "value", "org");

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateComplexField_when_conditional_rule_has_no_form_data_then_rule_is_skipped()
    {
        var conditionalEngine = Substitute.For<IConditionalLogicEngine>();
        var orchestrator = CreateExplicitOrchestrator(conditionalLogicEngine: conditionalEngine);
        var field = CreateComplexField("autocomplete", [ConditionalRequiredRule("Choose an organisation")]);

        var result = orchestrator.ValidateField(field, string.Empty, "org");

        Assert.True(result.IsValid);
        conditionalEngine.DidNotReceiveWithAnyArgs().EvaluateCondition(default!, default!, default);
    }

    [Fact]
    public void ValidateComplexField_when_condition_evaluation_throws_then_rule_is_skipped()
    {
        var conditionalEngine = Substitute.For<IConditionalLogicEngine>();
        conditionalEngine
            .EvaluateCondition(Arg.Any<Condition>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<ConditionalLogicContext?>())
            .Returns(_ => throw new InvalidOperationException("boom"));

        var orchestrator = CreateExplicitOrchestrator(conditionalLogicEngine: conditionalEngine);
        var field = CreateComplexField("autocomplete", [ConditionalRequiredRule("Choose an organisation")]);

        var result = orchestrator.ValidateField(field, string.Empty, new Dictionary<string, object>(), "org");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateComplexField_when_conditional_rule_condition_is_not_met_then_rule_is_skipped()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateComplexField("autocomplete", [ConditionalRequiredRule("Choose an organisation")]);

        var result = orchestrator.ValidateField(
            field,
            string.Empty,
            new Dictionary<string, object> { ["showExtra"] = "no" },
            "org");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateComplexField_when_conditional_rule_condition_is_met_then_rule_is_applied()
    {
        var orchestrator = CreateExplicitOrchestrator();
        var field = CreateComplexField("autocomplete", [ConditionalRequiredRule("Choose an organisation")]);

        var result = orchestrator.ValidateField(
            field,
            string.Empty,
            new Dictionary<string, object> { ["showExtra"] = "yes" },
            "org");

        Assert.False(result.IsValid);
        Assert.Equal("Choose an organisation", Assert.Single(result.Errors).Message);
    }

    private static FormValidationOrchestrator CreateRequiringOrchestrator(
        IConditionalLogicEngine? conditionalLogicEngine = null)
    {
        var fieldRequirementService = Substitute.For<IFieldRequirementService>();
        fieldRequirementService.IsFieldRequired(Arg.Any<Field>(), Arg.Any<FormTemplate>()).Returns(true);

        return CreateExplicitOrchestrator(fieldRequirementService, conditionalLogicEngine);
    }

    private static FormTemplate CreateTemplate(params Page[] pages)
    {
        List<TaskGroup> taskGroups = [];

        if (pages.Length > 0)
        {
            taskGroups.Add(new TaskGroup
            {
                GroupId = "g1",
                GroupName = "Group",
                GroupOrder = 1,
                GroupStatus = "NotStarted",
                Tasks =
                [
                    new Domain.Models.Task
                    {
                        TaskId = "task-1",
                        TaskName = "Task 1",
                        TaskOrder = 1,
                        TaskStatusString = "NotStarted",
                        Pages = pages.ToList()
                    }
                ]
            });
        }

        return new FormTemplate
        {
            TemplateId = "t1",
            TemplateName = "Template",
            Description = "Template",
            TaskGroups = taskGroups
        };
    }

    private static Field CreateField(string type, ValidationRule[]? validations = null) =>
        new()
        {
            FieldId = "field-1",
            Type = type,
            Label = new Label { Value = "Field 1" },
            Order = 1,
            Validations = validations?.ToList()
        };

    private static Field CreateComplexField(string complexFieldId, ValidationRule[]? validations = null)
    {
        var field = CreateField("complexField", validations);
        field.ComplexField = new ComplexField { Id = complexFieldId };
        return field;
    }

    private static ValidationRule Rule(string type, object rule, string message) =>
        new()
        {
            Type = type,
            Rule = rule,
            Message = message
        };

    private static ValidationRule ConditionalRequiredRule(string message) =>
        new()
        {
            Type = "required",
            Rule = string.Empty,
            Message = message,
            Condition = new Condition
            {
                TriggerField = "showExtra",
                Operator = ConditionalLogicConstants.Operators.Equals,
                Value = "yes",
                DataType = ConditionalLogicConstants.DataTypes.String
            }
        };

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