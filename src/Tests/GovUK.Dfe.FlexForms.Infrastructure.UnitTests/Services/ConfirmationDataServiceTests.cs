using GovUK.Dfe.FlexForms.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Services;

public class ConfirmationDataServiceTests
{
    private readonly ConfirmationDataService _service =
        new(NullLogger<ConfirmationDataService>.Instance);

    #region FormatDisplayData

    [Fact]
    public void FormatDisplayData_returns_empty_when_form_data_is_null()
    {
        var result = _service.FormatDisplayData(null!, Array.Empty<string>());

        Assert.Empty(result);
    }

    [Fact]
    public void FormatDisplayData_returns_empty_when_form_data_is_empty()
    {
        var result = _service.FormatDisplayData(new Dictionary<string, object>(), Array.Empty<string>());

        Assert.Empty(result);
    }

    [Fact]
    public void FormatDisplayData_shows_all_non_system_fields_when_display_fields_not_specified()
    {
        var formData = new Dictionary<string, object>
        {
            ["firstName"] = "Ada",
            ["handler"] = "Submit",
            ["__RequestVerificationToken"] = "token"
        };

        var result = _service.FormatDisplayData(formData, Array.Empty<string>());

        Assert.Single(result);
        Assert.Equal("Ada", result["First Name"]);
    }

    [Fact]
    public void FormatDisplayData_uses_display_fields_when_provided()
    {
        var formData = new Dictionary<string, object>
        {
            ["firstName"] = "Ada",
            ["lastName"] = "Lovelace",
            ["emailAddress"] = "ADA@EXAMPLE.COM"
        };

        var result = _service.FormatDisplayData(formData, ["firstName", "emailAddress"]);

        Assert.Equal(2, result.Count);
        Assert.Equal("Ada", result["First Name"]);
        Assert.Equal("ada@example.com", result["Email Address"]);
    }

    [Fact]
    public void FormatDisplayData_skips_blank_formatted_values()
    {
        var formData = new Dictionary<string, object>
        {
            ["firstName"] = "Ada",
            ["notes"] = "   "
        };

        var result = _service.FormatDisplayData(formData, ["firstName", "notes"]);

        Assert.Single(result);
        Assert.Equal("Ada", result["First Name"]);
    }

    [Fact]
    public void FormatDisplayData_augments_from_json_string_complex_field_value()
    {
        var formData = new Dictionary<string, object>
        {
            ["Data[EstablishmentComplexField]"] =
                "{\"name\":\"Example Trust\",\"ukprn\":\"10000001\",\"postcode\":\"sw1a1aa\",\"companiesHouseNumber\":\"abc123\"}"
        };

        var result = _service.FormatDisplayData(formData, ["trustName", "ukprn", "postcode", "companiesHouseNumber"]);

        Assert.Equal("Example Trust", result["Trust Name"]);
        Assert.Equal("10000001", result["UKPRN"]);
        Assert.Equal("SW1A1AA", result["Postcode"]);
        Assert.Equal("ABC123", result["Companies House Number"]);
    }

    [Fact]
    public void FormatDisplayData_augments_from_json_element_object()
    {
        using var doc = JsonDocument.Parse(
            "{\"name\":\"Json Trust\",\"ukprn\":10000002,\"postCode\":\"EC1A1BB\",\"companiesHousenumber\":\"xyz999\"}");
        var formData = new Dictionary<string, object>
        {
            ["complexField"] = doc.RootElement.Clone()
        };

        var result = _service.FormatDisplayData(formData, ["trustname", "ukprn", "postcode", "companiesHousenumber"]);

        Assert.Equal("Json Trust", result["Trust Name"]);
        Assert.Equal("10000002", result["UKPRN"]);
        Assert.Equal("EC1A1BB", result["Postcode"]);
        Assert.Equal("XYZ999", result["Companies House Number"]);
    }

    [Fact]
    public void FormatDisplayData_augments_postcode_from_nested_address_object()
    {
        var formData = new Dictionary<string, object>
        {
            ["establishment"] = "{\"name\":\"Nested School\",\"address\":{\"postalCode\":\"M1 1AE\"}}"
        };

        var result = _service.FormatDisplayData(formData, ["postcode"]);

        Assert.Equal("M1 1AE", result["Postcode"]);
    }

    [Fact]
    public void FormatDisplayData_ignores_malformed_json_augmentation()
    {
        var formData = new Dictionary<string, object>
        {
            ["establishment"] = "{not-json",
            ["firstName"] = "Ada"
        };

        var result = _service.FormatDisplayData(formData, ["firstName"]);

        Assert.Single(result);
        Assert.Equal("Ada", result["First Name"]);
    }

    [Fact]
    public void FormatDisplayData_does_not_add_missing_display_field()
    {
        var formData = new Dictionary<string, object> { ["firstName"] = "Ada" };

        var result = _service.FormatDisplayData(formData, ["missingField"]);

        Assert.Empty(result);
    }

    #endregion

    #region GetFieldDisplayName

    [Theory]
    [InlineData("trustName", "Trust Name")]
    [InlineData("ukprn", "UKPRN")]
    [InlineData("contributorEmail", "Email Address")]
    [InlineData("postcode", "Postcode")]
    public void GetFieldDisplayName_returns_known_mappings(string fieldName, string expected)
    {
        Assert.Equal(expected, _service.GetFieldDisplayName(fieldName));
    }

    [Fact]
    public void GetFieldDisplayName_converts_camel_case_to_title_case()
    {
        Assert.Equal("Address Line 1", _service.GetFieldDisplayName("addressLine1"));
    }

    [Fact]
    public void GetFieldDisplayName_returns_empty_for_blank_input()
    {
        Assert.Equal(string.Empty, _service.GetFieldDisplayName(string.Empty));
    }

    #endregion

    #region FormatFieldValue

    [Fact]
    public void FormatFieldValue_returns_empty_for_null_or_whitespace()
    {
        Assert.Equal(string.Empty, _service.FormatFieldValue("any", null!));
        Assert.Equal(string.Empty, _service.FormatFieldValue("any", "   "));
    }

    [Theory]
    [InlineData("ukprn", "10000001", "10000001")]
    [InlineData("ukprn", " 10000001 ", "10000001")]
    [InlineData("postcode", " sw1a 1aa ", "SW1A 1AA")]
    [InlineData("companiesHouseNumber", " ab123 ", "AB123")]
    [InlineData("emailAddress", " Ada@Example.COM ", "ada@example.com")]
    [InlineData("contributorEmail", " Ada@Example.COM ", "ada@example.com")]
    [InlineData("phoneNumber", " 01234 567890 ", "01234 567890")]
    public void FormatFieldValue_applies_field_specific_formatting(string fieldName, string input, string expected)
    {
        Assert.Equal(expected, _service.FormatFieldValue(fieldName, input));
    }

    [Fact]
    public void FormatFieldValue_trims_default_fields()
    {
        Assert.Equal("Some value", _service.FormatFieldValue("customField", "  Some value  "));
    }

    #endregion
}
