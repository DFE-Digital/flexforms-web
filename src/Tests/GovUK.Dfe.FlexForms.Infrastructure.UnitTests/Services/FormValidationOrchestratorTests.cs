using AutoFixture;
using AutoFixture.AutoNSubstitute;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Services;

public class FormValidationOrchestratorTests
{
    private readonly IFixture _fixture;
    private readonly FormValidationOrchestrator _orchestrator;

    public FormValidationOrchestratorTests()
    {
        _fixture = new Fixture().Customize(new AutoNSubstituteCustomization { ConfigureMembers = true });
        
        _fixture.Customize<Condition>(ob => ob.Without(rule => rule.Conditions));
        
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
        var modelState = _fixture.Create<ModelStateDictionary>();
        var fieldKey = field.FieldId;
        var formTemplate = _fixture.Create<FormTemplate>();

        var result = _orchestrator.ValidateField(field, submittedValue, formData, modelState, fieldKey, formTemplate);
        
        Assert.True(result);
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
        var modelState = _fixture.Create<ModelStateDictionary>();
        var fieldKey = field.FieldId;
        var formTemplate = _fixture.Create<FormTemplate>();

        var result = _orchestrator.ValidateField(field, submittedValue, formData, modelState, fieldKey, formTemplate);
        
        Assert.False(result);
        Assert.NotNull(modelState[fieldKey]);
        Assert.Equal("This field is required", modelState[fieldKey]!.Errors[0].ErrorMessage);
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
        var modelState = _fixture.Create<ModelStateDictionary>();
        var fieldKey = field.FieldId;
        var formTemplate = _fixture.Create<FormTemplate>();

        var result = _orchestrator.ValidateField(field, "not-an-option", formData, modelState, fieldKey, formTemplate);
        
        Assert.False(result);
        Assert.NotNull(modelState[fieldKey]);
        Assert.Equal("Select an option from the list", modelState[fieldKey]!.Errors[0].ErrorMessage);
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
        var modelState = _fixture.Create<ModelStateDictionary>();
        var fieldKey = field.FieldId;
        var formTemplate = _fixture.Create<FormTemplate>();

        var result = _orchestrator.ValidateField(field, submittedValue, formData, modelState, fieldKey, formTemplate);
        
        Assert.True(result);
        Assert.Null(modelState[fieldKey]);
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
        var modelState = new ModelStateDictionary();
        var fieldKey = field.FieldId;
        var formTemplate = _fixture.Create<FormTemplate>();

        // User sees five characters (EMAT + U+2019); submitted value may arrive as an HTML numeric character reference.
        const string submittedEncoded = "EMAT&#x2019;";

        var result = _orchestrator.ValidateField(field, submittedEncoded, formData, modelState, fieldKey, formTemplate);

        Assert.True(result);
        Assert.Null(modelState[fieldKey]);
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
        var modelState = new ModelStateDictionary();
        var fieldKey = field.FieldId;
        var formTemplate = _fixture.Create<FormTemplate>();

        const string submittedEncoded = "EMAT&#x2019;";

        var result = _orchestrator.ValidateField(field, submittedEncoded, formData, modelState, fieldKey, formTemplate);

        Assert.False(result);
        Assert.Equal("Too many characters", modelState[fieldKey]!.Errors[0].ErrorMessage);
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
        var modelState = new ModelStateDictionary();
        var fieldKey = field.FieldId;
        var formTemplate = _fixture.Create<FormTemplate>();

        var result = _orchestrator.ValidateField(field, sanitisedAsStored, formData, modelState, fieldKey, formTemplate);

        Assert.True(result);
        Assert.Null(modelState[fieldKey]);
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
        var modelState = new ModelStateDictionary();
        var fieldKey = field.FieldId;
        var formTemplate = _fixture.Create<FormTemplate>();

        var result = _orchestrator.ValidateField(field, sanitisedAsStored, formData, modelState, fieldKey, formTemplate);

        Assert.False(result);
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
        var modelState = new ModelStateDictionary();
        var fieldKey = field.FieldId;
        var formTemplate = _fixture.Create<FormTemplate>();

        return _orchestrator.ValidateField(field, sanitisedText, formData, modelState, fieldKey, formTemplate);
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
        var modelState = new ModelStateDictionary();
        var fieldKey = field.FieldId;
        var formTemplate = _fixture.Create<FormTemplate>();

        return _orchestrator.ValidateField(field, sanitisedText, formData, modelState, fieldKey, formTemplate);
    }
}