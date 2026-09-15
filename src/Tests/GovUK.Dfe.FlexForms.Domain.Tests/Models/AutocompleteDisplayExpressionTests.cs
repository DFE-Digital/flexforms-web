using GovUK.Dfe.FlexForms.Domain.Models;
using System.Text.Json;

namespace GovUK.Dfe.FlexForms.Domain.Tests.Models;

public class AutocompleteDisplayExpressionTests
{
    [Fact]
    public void Evaluate_concatenates_properties_and_literals()
    {
        var values = new Dictionary<string, object>
        {
            ["displayName"] = "Jane Smith",
            ["constituencyName"] = "Holborn"
        };

        var result = AutocompleteDisplayExpression.Evaluate(
            "displayName + \" - \" + constituencyName",
            values);

        Assert.Equal("Jane Smith - Holborn", result);
    }

    [Fact]
    public void Evaluate_skips_empty_properties_and_trailing_separators()
    {
        var values = new Dictionary<string, object>
        {
            ["displayName"] = "Jane Smith",
            ["constituencyName"] = ""
        };

        var result = AutocompleteDisplayExpression.Evaluate(
            "displayName + \" - \" + constituencyName",
            values);

        Assert.Equal("Jane Smith", result);
    }

    [Fact]
    public void Evaluate_supports_placeholder_syntax()
    {
        var values = new Dictionary<string, object>
        {
            ["firstName"] = "Jane",
            ["lastName"] = "Smith"
        };

        Assert.Equal("Jane Smith", AutocompleteDisplayExpression.Evaluate("{firstName} {lastName}", values));
    }

    [Fact]
    public void Evaluate_reads_json_element_properties()
    {
        using var doc = JsonDocument.Parse("""{"firstName":"Ada","lastName":"Lovelace"}""");

        var result = AutocompleteDisplayExpression.Evaluate("firstName + \" \" + lastName", doc.RootElement);

        Assert.Equal("Ada Lovelace", result);
    }

    [Fact]
    public void GetPropertyNames_returns_distinct_identifiers()
    {
        var names = AutocompleteDisplayExpression.GetPropertyNames("displayName + \" - \" + constituencyName");

        Assert.Equal(["displayName", "constituencyName"], names);
    }

    [Fact]
    public void Evaluate_returns_empty_when_expression_is_missing()
    {
        Assert.Equal(string.Empty, AutocompleteDisplayExpression.Evaluate(null, new Dictionary<string, object>()));
        Assert.False(AutocompleteDisplayExpression.IsSpecified("  "));
        Assert.Equal("template", AutocompleteDisplayExpression.FirstNonEmpty("template", "config"));
        Assert.Equal("config", AutocompleteDisplayExpression.FirstNonEmpty("  ", "config"));
    }

    [Fact]
    public void Evaluate_handles_null_values_and_case_insensitive_property_names()
    {
        var values = new Dictionary<string, object>
        {
            ["DisplayName"] = "Jane Smith",
            ["constituencyName"] = null!
        };

        Assert.Equal(
            "Jane Smith",
            AutocompleteDisplayExpression.Evaluate(
                "displayName + \" - \" + constituencyName",
                values));
        Assert.Equal(
            string.Empty,
            AutocompleteDisplayExpression.Evaluate("displayName", (IReadOnlyDictionary<string, object>?)null));
    }

    [Fact]
    public void Evaluate_json_supports_scalar_values_and_ignores_complex_values()
    {
        using var doc = JsonDocument.Parse(
            """{"name":"Result","rank":2,"active":true,"retired":false,"nested":{"name":"Ignored"},"items":[]}""");

        Assert.Equal(
            "Result / 2 / True / False",
            AutocompleteDisplayExpression.Evaluate(
                "name + \" / \" + rank + \" / \" + active + \" / \" + retired + \" / \" + nested + items",
                doc.RootElement));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("null")]
    public void Evaluate_json_returns_empty_for_non_object_values(string json)
    {
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(string.Empty, AutocompleteDisplayExpression.Evaluate("name", doc.RootElement));
    }

    [Fact]
    public void Evaluate_supports_single_quoted_and_unterminated_literals()
    {
        var values = new Dictionary<string, object> { ["name"] = "Jane" };

        Assert.Equal("Jane - Member", AutocompleteDisplayExpression.Evaluate("name + ' - Member'", values));
        Assert.Equal("Jane (Member", AutocompleteDisplayExpression.Evaluate("name + \" (Member", values));
    }

    [Fact]
    public void Evaluate_discards_leading_and_orphaned_separators()
    {
        var values = new Dictionary<string, object>
        {
            ["firstName"] = string.Empty,
            ["lastName"] = "Smith"
        };

        Assert.Equal(
            "Smith",
            AutocompleteDisplayExpression.Evaluate("firstName + \" - \" + lastName", values));
        Assert.Equal(string.Empty, AutocompleteDisplayExpression.Evaluate("+", values));
    }

    [Fact]
    public void GetPropertyNames_handles_missing_and_duplicate_names()
    {
        Assert.Empty(AutocompleteDisplayExpression.GetPropertyNames(null));
        Assert.Equal(
            ["name"],
            AutocompleteDisplayExpression.GetPropertyNames("name + \" / \" + NAME"));
    }
}
