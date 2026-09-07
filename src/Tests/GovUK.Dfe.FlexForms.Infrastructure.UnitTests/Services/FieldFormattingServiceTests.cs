using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Infrastructure.Services;
using NSubstitute;
using System.Text.Json;
using DomainTask = GovUK.Dfe.FlexForms.Domain.Models.Task;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Services;

public class FieldFormattingServiceTests
{
    private readonly IComplexFieldConfigurationService _configurationService =
        Substitute.For<IComplexFieldConfigurationService>();

    private FieldFormattingService CreateService() => new(_configurationService);

    #region GetFieldValue

    [Fact]
    public void GetFieldValue_returns_empty_when_field_is_missing()
    {
        var service = CreateService();

        var result = service.GetFieldValue("missing", new Dictionary<string, object>());

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetFieldValue_returns_empty_when_value_is_null()
    {
        var service = CreateService();
        var formData = new Dictionary<string, object> { ["field"] = null! };

        var result = service.GetFieldValue("field", formData);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetFieldValue_returns_string_value_unchanged()
    {
        var service = CreateService();
        var formData = new Dictionary<string, object> { ["field"] = "plain text" };

        var result = service.GetFieldValue("field", formData);

        Assert.Equal("plain text", result);
    }

    [Fact]
    public void GetFieldValue_serializes_non_string_object_to_json()
    {
        var service = CreateService();
        var formData = new Dictionary<string, object>
        {
            ["field"] = new Dictionary<string, string> { ["name"] = "Academy Trust", ["ukprn"] = "12345678" }
        };

        var result = service.GetFieldValue("field", formData);

        using var doc = JsonDocument.Parse(result);
        Assert.Equal("Academy Trust", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("12345678", doc.RootElement.GetProperty("ukprn").GetString());
    }

    [Fact]
    public void GetFieldValue_falls_back_to_to_string_when_serialization_fails()
    {
        var service = CreateService();
        var formData = new Dictionary<string, object> { ["field"] = new Action(() => { }) };

        var result = service.GetFieldValue("field", formData);

        Assert.Equal(formData["field"].ToString(), result);
    }

    #endregion

    #region GetFormattedFieldValue

    [Fact]
    public void GetFormattedFieldValue_returns_empty_for_missing_or_empty_values()
    {
        var service = CreateService();
        var formData = new Dictionary<string, object>
        {
            ["empty"] = string.Empty
        };

        Assert.Equal(string.Empty, service.GetFormattedFieldValue("missing", formData));
        Assert.Equal(string.Empty, service.GetFormattedFieldValue("empty", formData));
    }

    [Fact]
    public void GetFormattedFieldValue_returns_whitespace_unchanged_when_checkbox_normalization_is_empty()
    {
        var service = CreateService();
        var formData = new Dictionary<string, object> { ["whitespace"] = "   " };

        Assert.Equal("   ", service.GetFormattedFieldValue("whitespace", formData));
    }

    [Fact]
    public void GetFormattedFieldValue_returns_empty_for_placeholder_tokens()
    {
        var service = CreateService();
        var formData = new Dictionary<string, object> { ["field"] = "{placeholder}" };

        var result = service.GetFormattedFieldValue("field", formData);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetFormattedFieldValue_joins_checkbox_values_with_line_breaks()
    {
        var service = CreateService();
        var formData = new Dictionary<string, object>
        {
            ["choices"] = new[] { "Option A", "Option B" }
        };

        var result = service.GetFormattedFieldValue("choices", formData);

        Assert.Equal("Option A<br />Option B", result);
    }

    [Fact]
    public void GetFormattedFieldValue_joins_checkbox_json_array_string_with_line_breaks()
    {
        var service = CreateService();
        var formData = new Dictionary<string, object>
        {
            ["choices"] = "[\"Red\",\"Blue\"]"
        };

        var result = service.GetFormattedFieldValue("choices", formData);

        Assert.Equal("Red<br />Blue", result);
    }

    [Fact]
    public void GetFormattedFieldValue_returns_radio_value_as_plain_text()
    {
        var service = CreateService();
        var formData = new Dictionary<string, object> { ["decision"] = "Yes" };

        var result = service.GetFormattedFieldValue("decision", formData);

        Assert.Equal("Yes", result);
    }

    [Fact]
    public void GetFormattedFieldValue_returns_iso_date_unchanged_when_stored_as_plain_string()
    {
        var service = CreateService();
        var formData = new Dictionary<string, object> { ["startDate"] = "2025-03-15" };

        var result = service.GetFormattedFieldValue("startDate", formData);

        Assert.Equal("2025-03-15", result);
    }

    [Fact]
    public void GetFormattedFieldValue_formats_iso_date_when_reached_via_get_field_value_path()
    {
        var service = CreateService();

        var formatDate = typeof(FieldFormattingService).GetMethod(
            "TryFormatDate",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(formatDate);

        var formatted = (string)formatDate!.Invoke(service, ["2025-03-15"])!;

        Assert.Equal("15 March 2025", formatted);
    }

    [Fact]
    public void GetFormattedFieldValue_formats_autocomplete_object_with_name_and_ukprn()
    {
        var service = CreateService();
        var formData = new Dictionary<string, object>
        {
            ["establishment"] = "{\"name\":\"Example Academy\",\"ukprn\":\"10000001\"}"
        };

        var result = service.GetFormattedFieldValue("establishment", formData);

        Assert.Equal("Example Academy (UKPRN: 10000001)", result);
    }

    [Fact]
    public void GetFormattedFieldValue_formats_autocomplete_object_with_name_and_numeric_ukprn()
    {
        var service = CreateService();
        var formData = new Dictionary<string, object>
        {
            ["establishment"] = "{\"name\":\"Example Academy\",\"ukprn\":10000001}"
        };

        var result = service.GetFormattedFieldValue("establishment", formData);

        Assert.Equal("Example Academy (UKPRN: 10000001)", result);
    }

    [Fact]
    public void GetFormattedFieldValue_formats_autocomplete_object_with_name_and_code()
    {
        var service = CreateService();
        var formData = new Dictionary<string, object>
        {
            ["localAuthority"] = "{\"name\":\"Bristol\",\"code\":\"801\"}"
        };

        var result = service.GetFormattedFieldValue("localAuthority", formData);

        Assert.Equal("Bristol (Code: 801)", result);
    }

    [Fact]
    public void GetFormattedFieldValue_treats_json_array_string_as_checkbox_collection()
    {
        var service = CreateService();
        var formData = new Dictionary<string, object>
        {
            ["establishments"] =
                "[{\"name\":\"School A\",\"ukprn\":\"10000001\"},{\"name\":\"School B\",\"ukprn\":\"10000002\"}]"
        };

        var result = service.GetFormattedFieldValue("establishments", formData);

        Assert.Equal(
            "{\"name\":\"School A\",\"ukprn\":\"10000001\"}<br />{\"name\":\"School B\",\"ukprn\":\"10000002\"}",
            result);
    }

    [Fact]
    public void GetFormattedFieldValue_formats_upload_array_via_private_formatter()
    {
        var service = CreateService();
        const string uploadJson =
            "[{\"originalFileName\":\"evidence.pdf\",\"id\":\"f1\"},{\"originalFileName\":\"plan.docx\",\"id\":\"f2\"}]";

        var formatUpload = typeof(FieldFormattingService).GetMethod(
            "FormatUploadValue",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(formatUpload);

        var formatted = (string)formatUpload!.Invoke(service, [uploadJson])!;

        Assert.Equal("evidence.pdf<br />plan.docx", formatted);
    }

    [Fact]
    public void GetFormattedFieldValue_formats_upload_array_using_name_when_original_file_name_missing()
    {
        var service = CreateService();
        const string uploadJson = "[{\"name\":\"upload.txt\",\"fileSize\":1024,\"id\":\"f1\"}]";

        var formatUpload = typeof(FieldFormattingService).GetMethod(
            "FormatUploadValue",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(formatUpload);

        var formatted = (string)formatUpload!.Invoke(service, [uploadJson])!;

        Assert.Equal("upload.txt", formatted);
    }

    [Fact]
    public void GetFormattedFieldValue_returns_raw_value_for_malformed_json()
    {
        var service = CreateService();
        const string malformed = "{not valid json";
        var formData = new Dictionary<string, object> { ["field"] = malformed };

        var result = service.GetFormattedFieldValue("field", formData);

        Assert.Equal(malformed, result);
    }

    [Fact]
    public void GetFormattedFieldValue_skips_checkbox_normalization_for_json_object_string()
    {
        var service = CreateService();
        var formData = new Dictionary<string, object>
        {
            ["establishment"] = "{\"name\":\"Example Academy\",\"ukprn\":\"10000001\"}"
        };

        var result = service.GetFormattedFieldValue("establishment", formData);

        Assert.Equal("Example Academy (UKPRN: 10000001)", result);
    }

    [Fact]
    public void GetFormattedFieldValue_skips_checkbox_normalization_for_json_element_object()
    {
        var service = CreateService();
        using var doc = JsonDocument.Parse("{\"name\":\"Json Element Academy\",\"ukprn\":\"10000099\"}");
        var formData = new Dictionary<string, object> { ["establishment"] = doc.RootElement.Clone() };

        var result = service.GetFormattedFieldValue("establishment", formData);

        Assert.Equal("Json Element Academy (UKPRN: 10000099)", result);
    }

    [Fact]
    public void GetFormattedFieldValue_html_encodes_autocomplete_name_only()
    {
        var service = CreateService();
        var formData = new Dictionary<string, object>
        {
            ["establishment"] = "{\"name\":\"<script>alert(1)</script>\"}"
        };

        var result = service.GetFormattedFieldValue("establishment", formData);

        Assert.Equal("&lt;script&gt;alert(1)&lt;/script&gt;", result);
    }

    #endregion

    #region GetFormattedFieldValues

    [Fact]
    public void GetFormattedFieldValues_returns_empty_list_for_missing_or_placeholder_values()
    {
        var service = CreateService();
        var formData = new Dictionary<string, object> { ["field"] = "{token}" };

        Assert.Empty(service.GetFormattedFieldValues("missing", formData));
        Assert.Empty(service.GetFormattedFieldValues("field", formData));
    }

    [Fact]
    public void GetFormattedFieldValues_returns_checkbox_values_as_list()
    {
        var service = CreateService();
        var formData = new Dictionary<string, object>
        {
            ["choices"] = new[] { "One", "Two" }
        };

        var result = service.GetFormattedFieldValues("choices", formData);

        Assert.Equal(["One", "Two"], result);
    }

    [Fact]
    public void GetFormattedFieldValues_returns_checkbox_parsed_json_objects_for_array_string()
    {
        var service = CreateService();
        var formData = new Dictionary<string, object>
        {
            ["establishments"] = "[{\"name\":\"School A\",\"ukprn\":\"1\"},{\"name\":\"School B\",\"ukprn\":\"2\"}]"
        };

        var result = service.GetFormattedFieldValues("establishments", formData);

        Assert.Equal(["{\"name\":\"School A\",\"ukprn\":\"1\"}", "{\"name\":\"School B\",\"ukprn\":\"2\"}"], result);
    }

    [Fact]
    public void GetFormattedFieldValues_formats_autocomplete_array_via_private_formatter()
    {
        var service = CreateService();
        const string autocompleteJson =
            "[{\"name\":\"School A\",\"ukprn\":\"1\"},{\"name\":\"School B\",\"ukprn\":\"2\"}]";

        var formatAutocompleteList = typeof(FieldFormattingService).GetMethod(
            "FormatAutocompleteValuesList",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(formatAutocompleteList);

        var formatted = (List<string>)formatAutocompleteList!.Invoke(service, [autocompleteJson])!;

        Assert.Equal(["School A (UKPRN: 1)", "School B (UKPRN: 2)"], formatted);
    }

    [Fact]
    public void GetFormattedFieldValues_formats_upload_names_via_private_formatter()
    {
        var service = CreateService();
        const string uploadJson = "[{\"originalFileName\":\"a.pdf\"},{\"originalFileName\":\"b.pdf\"}]";

        var formatUploadList = typeof(FieldFormattingService).GetMethod(
            "FormatUploadValuesList",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(formatUploadList);

        var formatted = (List<string>)formatUploadList!.Invoke(service, [uploadJson])!;

        Assert.Equal(["a.pdf", "b.pdf"], formatted);
    }

    [Fact]
    public void GetFormattedFieldValues_returns_single_item_for_plain_text()
    {
        var service = CreateService();
        var formData = new Dictionary<string, object> { ["notes"] = "Some notes" };

        var result = service.GetFormattedFieldValues("notes", formData);

        Assert.Equal(["Some notes"], result);
    }

    [Fact]
    public void GetFormattedFieldValues_returns_single_item_for_malformed_json()
    {
        var service = CreateService();
        const string malformed = "[not json";
        var formData = new Dictionary<string, object> { ["field"] = malformed };

        var result = service.GetFormattedFieldValues("field", formData);

        Assert.Equal([malformed], result);
    }

    #endregion

    #region GetFieldItemLabel / IsFieldAllowMultiple

    [Fact]
    public void GetFieldItemLabel_returns_configuration_label_for_complex_field()
    {
        _configurationService.GetConfiguration("establishment").Returns(new ComplexFieldConfiguration
        {
            Id = "establishment",
            Label = "Establishment"
        });

        var service = CreateService();
        var template = CreateTemplate("complexField", "establishment");

        var result = service.GetFieldItemLabel("complexField", template);

        Assert.Equal("Establishment", result);
    }

    [Fact]
    public void GetFieldItemLabel_returns_default_when_field_not_found()
    {
        var service = CreateService();

        Assert.Equal("Item", service.GetFieldItemLabel("missing", CreateTemplate("other", "establishment")));
        Assert.Equal("Item", service.GetFieldItemLabel("complexField", null!));
    }

    [Fact]
    public void IsFieldAllowMultiple_returns_configuration_value_for_complex_field()
    {
        _configurationService.GetConfiguration("establishment").Returns(new ComplexFieldConfiguration
        {
            Id = "establishment",
            AllowMultiple = true
        });

        var service = CreateService();
        var template = CreateTemplate("complexField", "establishment");

        Assert.True(service.IsFieldAllowMultiple("complexField", template));
    }

    [Fact]
    public void IsFieldAllowMultiple_returns_false_when_field_not_found()
    {
        var service = CreateService();

        Assert.False(service.IsFieldAllowMultiple("missing", CreateTemplate("other", "establishment")));
    }

    #endregion

    #region HasFieldValue

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("value", true)]
    public void HasFieldValue_reflects_non_whitespace_field_content(string storedValue, bool expected)
    {
        var service = CreateService();
        var formData = new Dictionary<string, object> { ["field"] = storedValue };

        Assert.Equal(expected, service.HasFieldValue("field", formData));
        Assert.False(service.HasFieldValue("missing", formData));
    }

    #endregion

    private static FormTemplate CreateTemplate(string fieldId, string complexFieldId) =>
        new()
        {
            TemplateId = "t1",
            TemplateName = "Template",
            Description = "Description",
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
                        new DomainTask
                        {
                            TaskId = "task1",
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
                                    Description = "Page description",
                                    PageOrder = 1,
                                    Fields =
                                    [
                                        new Field
                                        {
                                            FieldId = fieldId,
                                            Type = "complex",
                                            Label = new Label { Value = "Label" },
                                            Order = 1,
                                            ComplexField = new ComplexField { Id = complexFieldId }
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
