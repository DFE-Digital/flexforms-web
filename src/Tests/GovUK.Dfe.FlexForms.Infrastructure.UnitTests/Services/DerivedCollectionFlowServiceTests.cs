using AutoFixture;
using GovUK.Dfe.CoreLibs.Testing.AutoFixture.Customizations;
using GovUK.Dfe.CoreLibs.Testing.Helpers;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Infrastructure.Services;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Services;

public class DerivedCollectionFlowServiceTests
{
    private readonly IFixture _fixture;
    private readonly DerivedCollectionFlowService _service;

    public DerivedCollectionFlowServiceTests()
    {
        _fixture = FixtureFactoryHelper.ConfigureFixtureFactory([
            typeof(NSubstituteWithMembersCustomization),
            typeof(OmitCircularReferenceCustomization)
        ]);
        
        _fixture.Customize<Condition>(ob => ob.Without(rule => rule.Conditions));
        
        _service = _fixture.Create<DerivedCollectionFlowService>();
    }
    
    [Fact]
    public void GenerateItemsFromSourceField_for_collection_correctly_decodes_sourceJson()
    {
        var fieldId = _fixture.Create<string>();
        var sourceJson = "[{\"foo\":{\"id\":\"123456\",\"name\":\"some foo\"},\"bar\":123}]";
        var formData = new Dictionary<string, object>
        {
            [fieldId] = sourceJson,
        };
        var config = _fixture.Build<DerivedCollectionFlowConfiguration>()
            .With(c => c.SourceType, "collection")
            .Create();

        var result = _service.GenerateItemsFromSourceField(fieldId, formData, config);
        
        Assert.NotEmpty(result);
        var item = Assert.Single(result);
        Assert.Equal("123456", item.Id);
        Assert.Equal("some foo", item.DisplayName);
        Assert.Equal("Not signed yet", item.Status);
    }
    
    [Fact]
    public void GenerateItemsFromSourceField_for_collection_correctly_html_escapes_sourceJson()
    {
        var fieldId = _fixture.Create<string>();
        var sourceJson = "[{\"foo\":\"{&quot;id&quot;:&quot;123456&quot;,&quot;name&quot;:&quot;some foo&quot;}\",\"bar\":123}]";
        var formData = new Dictionary<string, object>
        {
            [fieldId] = sourceJson,
        };
        var config = _fixture.Build<DerivedCollectionFlowConfiguration>()
            .With(c => c.SourceType, "collection")
            .Create();

        var result = _service.GenerateItemsFromSourceField(fieldId, formData, config);
        
        Assert.NotEmpty(result);
        var item = Assert.Single(result);
        Assert.Equal("123456", item.Id);
        Assert.Equal("some foo", item.DisplayName);
        Assert.Equal("Not signed yet", item.Status);
    }

    [Theory]
    [InlineData("[{\"foo\":\"{&quot;id&quot;:&quot;123456&quot;,&quot;name&quot;:&quot;some foo&quot;}\",\"bar\":123,\"Data[foo]\":\"{\\\"id\\\":\\\"456789\\\",\\\"name\\\":\\\"another foo\\\"}\"}]")]
    [InlineData("[{\"foo\":\"{&quot;id&quot;:&quot;123456&quot;,&quot;name&quot;:&quot;some foo&quot;}\",\"bar\":123,\"Data_foo\":\"{\\\"id\\\":\\\"456789\\\",\\\"name\\\":\\\"another foo\\\"}\"}]")]
    public void GenerateItemsFromSourceField_does_not_return_duplicate_entries_when_source_collection_is_modified(string sourceJson)
    {
        // A derived collection is dependent on a collection field, e.g. `trustsSearch-field-flow`.
        // When a user adds a collection item, the `sourceJson` for the derived collection contains a value with that
        // key.
        // When a user modifies the collection item, an additional key is added to the `sourceJson` (in the case of the
        // example above, `Data[trustsSearch-field-flow]` or `Data_trustsSearch-field-flow`).
        // This causes the item to be duplicated in the returned list, so keys of these formats need to be ignored.
        
        var fieldId = _fixture.Create<string>();
        var formData = new Dictionary<string, object>
        {
            [fieldId] = sourceJson,
        };
        var config = _fixture.Build<DerivedCollectionFlowConfiguration>()
            .With(c => c.SourceType, "collection")
            .Create();

        var result = _service.GenerateItemsFromSourceField(fieldId, formData, config);
        
        Assert.NotEmpty(result);
        var item = Assert.Single(result);
        Assert.Equal("123456", item.Id);
        Assert.Equal("some foo", item.DisplayName);
        Assert.Equal("Not signed yet", item.Status);
    }

    private DerivedCollectionFlowConfiguration CreateConfig(string? sourceType, string? itemTitleBinding = "name")
    {
        var config = _fixture.Build<DerivedCollectionFlowConfiguration>()
            .Without(c => c.Pages)
            .Create();
        config.SourceType = sourceType!;
        config.ItemTitleBinding = itemTitleBinding!;
        return config;
    }

    [Fact]
    public void GenerateItemsFromSourceField_returns_empty_when_source_field_is_absent_from_formData()
    {
        var config = CreateConfig("autocomplete");

        var result = _service.GenerateItemsFromSourceField("missing-field", new Dictionary<string, object>(), config);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    public void GenerateItemsFromSourceField_returns_empty_when_source_value_is_blank(string sourceValue)
    {
        var fieldId = _fixture.Create<string>();
        var formData = new Dictionary<string, object> { [fieldId] = sourceValue };
        var config = CreateConfig("autocomplete");

        var result = _service.GenerateItemsFromSourceField(fieldId, formData, config);

        Assert.Empty(result);
    }

    [Fact]
    public void GenerateItemsFromSourceField_returns_empty_when_source_value_is_null()
    {
        var fieldId = _fixture.Create<string>();
        var formData = new Dictionary<string, object> { [fieldId] = null! };
        var config = CreateConfig("autocomplete");

        var result = _service.GenerateItemsFromSourceField(fieldId, formData, config);

        Assert.Empty(result);
    }

    [Fact]
    public void GenerateItemsFromSourceField_returns_empty_when_processing_throws()
    {
        // A null title binding is used as a dictionary key when building prefilled data, which throws.
        // The service is expected to swallow the failure and degrade to an empty collection.
        var fieldId = _fixture.Create<string>();
        var formData = new Dictionary<string, object> { [fieldId] = "[\"option1\"]" };
        var config = CreateConfig("checkboxes", itemTitleBinding: null);

        var result = _service.GenerateItemsFromSourceField(fieldId, formData, config);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData("autocomplete")]
    [InlineData("AUTOCOMPLETE")]
    public void GenerateItemsFromSourceField_for_autocomplete_returns_one_item_per_object(string sourceType)
    {
        var fieldId = _fixture.Create<string>();
        var formData = new Dictionary<string, object>
        {
            [fieldId] = "[{\"name\":\"Trust A\"},{\"name\":\"Trust B\"}]",
        };
        var config = CreateConfig(sourceType);

        var result = _service.GenerateItemsFromSourceField(fieldId, formData, config);

        Assert.Equal(2, result.Count);
        Assert.Equal(["trust-a", "trust-b"], result.Select(i => i.Id));
        Assert.Equal(["Trust A", "Trust B"], result.Select(i => i.DisplayName));
        Assert.All(result, item => Assert.Equal("Not signed yet", item.Status));
    }

    [Fact]
    public void GenerateItemsFromSourceField_for_autocomplete_copies_source_fields_into_prefilled_data()
    {
        var fieldId = _fixture.Create<string>();
        var formData = new Dictionary<string, object>
        {
            [fieldId] = "[{\"name\":\"Trust A\",\"ukprn\":\"12345678\"}]",
        };
        var config = CreateConfig("autocomplete");

        var item = Assert.Single(_service.GenerateItemsFromSourceField(fieldId, formData, config));

        Assert.Equal("Trust A", item.PrefilledData["name"]?.ToString());
        Assert.Equal("12345678", item.PrefilledData["ukprn"]?.ToString());
        Assert.NotNull(item.SourceData);
        Assert.Equal("12345678", item.SourceData!["ukprn"]?.ToString());
    }

    [Fact]
    public void GenerateItemsFromSourceField_for_autocomplete_falls_back_to_common_field_names_when_binding_is_absent()
    {
        var fieldId = _fixture.Create<string>();
        var formData = new Dictionary<string, object>
        {
            [fieldId] = "[{\"title\":\"Some Title\"}]",
        };
        var config = CreateConfig("autocomplete", itemTitleBinding: "unmatched-binding");

        var item = Assert.Single(_service.GenerateItemsFromSourceField(fieldId, formData, config));

        Assert.Equal("Some Title", item.DisplayName);
        Assert.Equal("Some Title", item.PrefilledData["unmatched-binding"]?.ToString());
    }

    [Fact]
    public void GenerateItemsFromSourceField_for_autocomplete_falls_back_to_first_value_when_no_known_field_names_match()
    {
        var fieldId = _fixture.Create<string>();
        var formData = new Dictionary<string, object>
        {
            [fieldId] = "[{\"reference\":\"REF-1\"}]",
        };
        var config = CreateConfig("autocomplete", itemTitleBinding: "unmatched-binding");

        var item = Assert.Single(_service.GenerateItemsFromSourceField(fieldId, formData, config));

        Assert.Equal("REF-1", item.DisplayName);
        Assert.Equal("ref-1", item.Id);
    }

    [Fact]
    public void GenerateItemsFromSourceField_for_autocomplete_skips_objects_without_a_display_name()
    {
        var fieldId = _fixture.Create<string>();
        var formData = new Dictionary<string, object>
        {
            [fieldId] = "[{\"name\":\"   \"},{\"name\":\"Trust B\"},{\"other\":null}]",
        };
        var config = CreateConfig("autocomplete");

        var item = Assert.Single(_service.GenerateItemsFromSourceField(fieldId, formData, config));

        Assert.Equal("Trust B", item.DisplayName);
    }

    [Fact]
    public void GenerateItemsFromSourceField_for_autocomplete_returns_empty_when_json_is_null_literal()
    {
        var fieldId = _fixture.Create<string>();
        var formData = new Dictionary<string, object> { [fieldId] = "null" };
        var config = CreateConfig("autocomplete");

        Assert.Empty(_service.GenerateItemsFromSourceField(fieldId, formData, config));
    }

    [Fact]
    public void GenerateItemsFromSourceField_for_autocomplete_falls_back_to_string_array_parsing()
    {
        var fieldId = _fixture.Create<string>();
        var formData = new Dictionary<string, object> { [fieldId] = "[\"Trust A\",\"Trust B\"]" };
        var config = CreateConfig("autocomplete");

        var result = _service.GenerateItemsFromSourceField(fieldId, formData, config);

        Assert.Equal(["Trust A", "Trust B"], result.Select(i => i.DisplayName));
        Assert.Equal("Trust A", result[0].PrefilledData["name"]?.ToString());
    }

    [Fact]
    public void GenerateItemsFromSourceField_for_checkboxes_returns_one_item_per_selected_option()
    {
        var fieldId = _fixture.Create<string>();
        var formData = new Dictionary<string, object> { [fieldId] = "[\"option1\",\"option2\"]" };
        var config = CreateConfig("checkboxes", itemTitleBinding: "choice");

        var result = _service.GenerateItemsFromSourceField(fieldId, formData, config);

        Assert.Equal(["option1", "option2"], result.Select(i => i.DisplayName));
        Assert.Equal(["option1", "option2"], result.Select(i => i.Id));
        Assert.Equal("option1", result[0].PrefilledData["choice"]?.ToString());
        Assert.Null(result[0].SourceData);
    }

    [Fact]
    public void GenerateItemsFromSourceField_for_checkboxes_skips_blank_options()
    {
        var fieldId = _fixture.Create<string>();
        var formData = new Dictionary<string, object> { [fieldId] = "[\"option1\",\"\",\"   \"]" };
        var config = CreateConfig("checkboxes");

        var item = Assert.Single(_service.GenerateItemsFromSourceField(fieldId, formData, config));

        Assert.Equal("option1", item.DisplayName);
    }

    [Fact]
    public void GenerateItemsFromSourceField_for_checkboxes_returns_empty_when_json_is_null_literal()
    {
        var fieldId = _fixture.Create<string>();
        var formData = new Dictionary<string, object> { [fieldId] = "null" };
        var config = CreateConfig("checkboxes");

        Assert.Empty(_service.GenerateItemsFromSourceField(fieldId, formData, config));
    }

    [Fact]
    public void GenerateItemsFromSourceField_for_checkboxes_falls_back_to_comma_separated_parsing()
    {
        var fieldId = _fixture.Create<string>();
        var formData = new Dictionary<string, object> { [fieldId] = "option1, option2 ,option3" };
        var config = CreateConfig("checkboxes");

        var result = _service.GenerateItemsFromSourceField(fieldId, formData, config);

        Assert.Equal(["option1", "option2", "option3"], result.Select(i => i.DisplayName));
    }

    [Theory]
    [InlineData("Trust A & B", "trust-a-b")]
    [InlineData("Trust A!", "trust-a")]
    public void GenerateItemsFromSourceField_slugifies_the_display_name_into_the_item_id(string option, string expectedId)
    {
        var fieldId = _fixture.Create<string>();
        var formData = new Dictionary<string, object> { [fieldId] = $"[\"{option}\"]" };
        var config = CreateConfig("checkboxes");

        var item = Assert.Single(_service.GenerateItemsFromSourceField(fieldId, formData, config));

        Assert.Equal(expectedId, item.Id);
    }

    [Fact]
    public void GenerateItemsFromSourceField_generates_a_random_item_id_when_display_name_has_no_slug_characters()
    {
        var fieldId = _fixture.Create<string>();
        var formData = new Dictionary<string, object> { [fieldId] = "[\"!!!\"]" };
        var config = CreateConfig("checkboxes");

        var item = Assert.Single(_service.GenerateItemsFromSourceField(fieldId, formData, config));

        Assert.Equal("!!!", item.DisplayName);
        Assert.Matches("^[0-9a-f]{8}$", item.Id);
    }

    [Fact]
    public void GenerateItemsFromSourceField_truncates_long_item_ids_to_50_characters()
    {
        var fieldId = _fixture.Create<string>();
        var formData = new Dictionary<string, object> { [fieldId] = $"[\"{new string('a', 60)}\"]" };
        var config = CreateConfig("checkboxes");

        var item = Assert.Single(_service.GenerateItemsFromSourceField(fieldId, formData, config));

        Assert.Equal(new string('a', 50), item.Id);
    }

    [Fact]
    public void GenerateItemsFromSourceField_trims_trailing_hyphens_from_truncated_item_ids()
    {
        var fieldId = _fixture.Create<string>();
        var formData = new Dictionary<string, object> { [fieldId] = $"[\"{new string('a', 49)} bb\"]" };
        var config = CreateConfig("checkboxes");

        var item = Assert.Single(_service.GenerateItemsFromSourceField(fieldId, formData, config));

        Assert.Equal(new string('a', 49), item.Id);
    }

    [Theory]
    [InlineData("[{\"id\":\"123\",\"_metadata\":\"{\\\"x\\\":1}\"}]")]
    [InlineData("[{\"foo\":\"not-an-object\"}]")]
    [InlineData("[{\"foo\":null}]")]
    [InlineData("[{\"foo\":\"{not-valid-json\"}]")]
    [InlineData("null")]
    [InlineData("not-json-at-all")]
    public void GenerateItemsFromSourceField_for_collection_returns_empty_when_no_nested_object_can_be_read(string sourceJson)
    {
        var fieldId = _fixture.Create<string>();
        var formData = new Dictionary<string, object> { [fieldId] = sourceJson };
        var config = CreateConfig("collection");

        Assert.Empty(_service.GenerateItemsFromSourceField(fieldId, formData, config));
    }

    [Fact]
    public void GenerateItemsFromSourceField_for_collection_generates_item_id_when_nested_object_has_no_id()
    {
        var fieldId = _fixture.Create<string>();
        var formData = new Dictionary<string, object>
        {
            [fieldId] = "[{\"foo\":\"{\\\"name\\\":\\\"Some Foo\\\"}\"}]",
        };
        var config = CreateConfig("collection");

        var item = Assert.Single(_service.GenerateItemsFromSourceField(fieldId, formData, config));

        Assert.Equal("some-foo", item.Id);
        Assert.Equal("Some Foo", item.DisplayName);
    }

    [Fact]
    public void GenerateItemsFromSourceField_for_collection_uses_numeric_nested_id()
    {
        var fieldId = _fixture.Create<string>();
        var formData = new Dictionary<string, object>
        {
            [fieldId] = "[{\"foo\":\"{\\\"id\\\":123456,\\\"name\\\":\\\"Some Foo\\\"}\"}]",
        };
        var config = CreateConfig("collection");

        var item = Assert.Single(_service.GenerateItemsFromSourceField(fieldId, formData, config));

        Assert.Equal("123456", item.Id);
    }

    [Fact]
    public void GenerateItemsFromSourceField_for_collection_returns_an_item_for_every_nested_object_across_items()
    {
        var fieldId = _fixture.Create<string>();
        var formData = new Dictionary<string, object>
        {
            [fieldId] = "[{\"foo\":\"{\\\"id\\\":\\\"1\\\",\\\"name\\\":\\\"First\\\"}\"}," +
                        "{\"foo\":\"{\\\"id\\\":\\\"2\\\",\\\"name\\\":\\\"Second\\\"}\"}]",
        };
        var config = CreateConfig("collection");

        var result = _service.GenerateItemsFromSourceField(fieldId, formData, config);

        Assert.Equal(["1", "2"], result.Select(i => i.Id));
        Assert.Equal(["First", "Second"], result.Select(i => i.DisplayName));
    }

    [Fact]
    public void GenerateItemsFromSourceField_for_select_splits_comma_separated_values()
    {
        var fieldId = _fixture.Create<string>();
        var formData = new Dictionary<string, object> { [fieldId] = "Trust A, Trust B ,Trust C" };
        var config = CreateConfig("select", itemTitleBinding: "trust");

        var result = _service.GenerateItemsFromSourceField(fieldId, formData, config);

        Assert.Equal(["Trust A", "Trust B", "Trust C"], result.Select(i => i.DisplayName));
        Assert.Equal(["trust-a", "trust-b", "trust-c"], result.Select(i => i.Id));
        Assert.Equal("Trust A", result[0].PrefilledData["trust"]?.ToString());
    }

    [Fact]
    public void GenerateItemsFromSourceField_for_select_returns_a_single_item_for_a_single_value()
    {
        var fieldId = _fixture.Create<string>();
        var formData = new Dictionary<string, object> { [fieldId] = "Trust A" };
        var config = CreateConfig("select");

        var item = Assert.Single(_service.GenerateItemsFromSourceField(fieldId, formData, config));

        Assert.Equal("Trust A", item.DisplayName);
    }

    [Fact]
    public void GenerateItemsFromSourceField_for_select_returns_empty_when_all_values_are_blank()
    {
        var fieldId = _fixture.Create<string>();
        var formData = new Dictionary<string, object> { [fieldId] = " , , " };
        var config = CreateConfig("select");

        Assert.Empty(_service.GenerateItemsFromSourceField(fieldId, formData, config));
    }

    [Theory]
    [InlineData("unrecognised-source-type")]
    [InlineData(null)]
    public void GenerateItemsFromSourceField_for_unknown_source_type_parses_a_string_array(string? sourceType)
    {
        var fieldId = _fixture.Create<string>();
        var formData = new Dictionary<string, object> { [fieldId] = "[\"Trust A\",\"Trust B\"]" };
        var config = CreateConfig(sourceType);

        var result = _service.GenerateItemsFromSourceField(fieldId, formData, config);

        Assert.Equal(["Trust A", "Trust B"], result.Select(i => i.DisplayName));
    }

    [Fact]
    public void GenerateItemsFromSourceField_for_unknown_source_type_falls_back_to_a_single_raw_value()
    {
        var fieldId = _fixture.Create<string>();
        var formData = new Dictionary<string, object> { [fieldId] = "Trust A" };
        var config = CreateConfig("unrecognised-source-type");

        var item = Assert.Single(_service.GenerateItemsFromSourceField(fieldId, formData, config));

        Assert.Equal("Trust A", item.DisplayName);
        Assert.Equal("trust-a", item.Id);
    }

    [Fact]
    public void GetItemStatuses_returns_statuses_for_matching_keys_only()
    {
        var formData = new Dictionary<string, object>
        {
            ["signatures_status_item-1"] = "Signed",
            ["signatures_status_item-2"] = "Not signed yet",
            ["signatures_data_item-1"] = "{}",
            ["other_status_item-3"] = "Signed",
        };

        var statuses = _service.GetItemStatuses("signatures", formData);

        Assert.Equal(2, statuses.Count);
        Assert.Equal("Signed", statuses["item-1"]);
        Assert.Equal("Not signed yet", statuses["item-2"]);
    }

    [Fact]
    public void GetItemStatuses_defaults_to_not_signed_yet_when_stored_status_is_null()
    {
        var formData = new Dictionary<string, object> { ["signatures_status_item-1"] = null! };

        var statuses = _service.GetItemStatuses("signatures", formData);

        Assert.Equal("Not signed yet", statuses["item-1"]);
    }

    [Fact]
    public void GetItemStatuses_returns_empty_when_form_data_has_no_status_keys()
    {
        var statuses = _service.GetItemStatuses("signatures", new Dictionary<string, object>());

        Assert.Empty(statuses);
    }

    [Fact]
    public void GetItemDeclarationData_returns_empty_when_declaration_key_is_absent()
    {
        var result = _service.GetItemDeclarationData("signatures", "item-1", new Dictionary<string, object>());

        Assert.Empty(result);
    }

    [Fact]
    public void GetItemDeclarationData_returns_deserialized_declaration()
    {
        var formData = new Dictionary<string, object>
        {
            ["signatures_data_item-1"] = "{\"signatoryName\":\"Alice\"}",
        };

        var result = _service.GetItemDeclarationData("signatures", "item-1", formData);

        Assert.Equal("Alice", result["signatoryName"]?.ToString());
    }

    [Fact]
    public void GetItemDeclarationData_returns_empty_when_stored_declaration_is_null()
    {
        var formData = new Dictionary<string, object> { ["signatures_data_item-1"] = null! };

        Assert.Empty(_service.GetItemDeclarationData("signatures", "item-1", formData));
    }

    [Fact]
    public void GetItemDeclarationData_returns_empty_when_stored_declaration_is_not_valid_json()
    {
        var formData = new Dictionary<string, object> { ["signatures_data_item-1"] = "{not-valid-json" };

        Assert.Empty(_service.GetItemDeclarationData("signatures", "item-1", formData));
    }

    [Fact]
    public void SaveItemDeclaration_writes_status_and_serialized_declaration_into_form_data()
    {
        var formData = new Dictionary<string, object>();
        var declaration = new Dictionary<string, object> { ["signatoryName"] = "Alice" };

        _service.SaveItemDeclaration("signatures", "item-1", declaration, "Signed", formData);

        Assert.Equal("Signed", formData["signatures_status_item-1"]?.ToString());
        Assert.Equal("{\"signatoryName\":\"Alice\"}", formData["signatures_data_item-1"]?.ToString());
    }

    [Fact]
    public void SaveItemDeclaration_writes_values_that_can_be_read_back_by_the_getters()
    {
        var formData = new Dictionary<string, object>();
        var declaration = new Dictionary<string, object> { ["signatoryName"] = "Alice" };

        _service.SaveItemDeclaration("signatures", "item-1", declaration, "Signed", formData);

        Assert.Equal("Signed", _service.GetItemStatuses("signatures", formData)["item-1"]);
        Assert.Equal(
            "Alice",
            _service.GetItemDeclarationData("signatures", "item-1", formData)["signatoryName"]?.ToString());
    }

    [Fact]
    public void SaveItemDeclaration_overwrites_a_previously_saved_declaration()
    {
        var formData = new Dictionary<string, object>
        {
            ["signatures_status_item-1"] = "Not signed yet",
            ["signatures_data_item-1"] = "{\"signatoryName\":\"Alice\"}",
        };

        _service.SaveItemDeclaration(
            "signatures",
            "item-1",
            new Dictionary<string, object> { ["signatoryName"] = "Bob" },
            "Signed",
            formData);

        Assert.Equal("Signed", formData["signatures_status_item-1"]?.ToString());
        Assert.Equal("{\"signatoryName\":\"Bob\"}", formData["signatures_data_item-1"]?.ToString());
    }
}