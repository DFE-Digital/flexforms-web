using GovUK.Dfe.FlexForms.Web.ViewModels.FormEngine;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.ViewModels.FormEngine;

public class AutocompleteSummaryFormatterTests
{
    [Fact]
    public void Render_returns_empty_for_blank_input()
    {
        Assert.Equal(string.Empty, AutocompleteSummaryFormatter.Render(null));
        Assert.Equal(string.Empty, AutocompleteSummaryFormatter.Render("  "));
    }

    [Fact]
    public void Render_formats_name_postcode_ukprn_and_companies_house()
    {
        var json = """
            {
              "name": "Contoso Trust",
              "postcode": "SW1A 1AA",
              "ukprn": "12345678",
              "companiesHouseNumber": "AB123456"
            }
            """;

        var html = AutocompleteSummaryFormatter.Render(json);

        Assert.Contains("govuk-!-font-weight-bold", html);
        Assert.Contains("Contoso Trust", html);
        Assert.Contains("Postcode: SW1A 1AA", html);
        Assert.Contains("UKPRN: 12345678", html);
        Assert.Contains("Companies house number: AB123456", html);
    }

    [Fact]
    public void Render_reads_nested_address_postcode()
    {
        var json = """{"name":"School","address":{"postalCode":"M1 1AA"}}""";

        var html = AutocompleteSummaryFormatter.Render(json);

        Assert.Contains("Postcode: M1 1AA", html);
    }

    [Fact]
    public void Render_html_encodes_non_json_and_invalid_json()
    {
        Assert.Equal("&lt;script&gt;", AutocompleteSummaryFormatter.Render("<script>"));
        Assert.Equal("not-json", AutocompleteSummaryFormatter.Render("not-json"));
    }

    [Fact]
    public void TryFindJsonInItem_returns_object_with_name_or_ukprn()
    {
        var json = """{"name":"Found"}""";
        var item = new Dictionary<string, object>
        {
            ["other"] = "plain",
            ["org"] = json
        };

        Assert.Equal(json, AutocompleteSummaryFormatter.TryFindJsonInItem(item));
        Assert.Equal(string.Empty, AutocompleteSummaryFormatter.TryFindJsonInItem(new Dictionary<string, object> { ["x"] = "nope" }));
    }
}
