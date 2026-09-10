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
}
