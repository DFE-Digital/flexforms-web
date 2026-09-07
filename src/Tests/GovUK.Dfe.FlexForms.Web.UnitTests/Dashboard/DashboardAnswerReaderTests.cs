using System.Text;
using System.Text.Json;
using GovUK.Dfe.FlexForms.Application.Dashboard;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Dashboard;

public class DashboardAnswerReaderTests
{
    [Fact]
    public void GetDisplayValue_ReadsSimpleField()
    {
        var body = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["incomingTrustName"] = "Example Trust"
        });

        var formData = DashboardAnswerReader.ParseFormData(body);
        var value = DashboardAnswerReader.GetDisplayValue("incomingTrustName", formData);

        Assert.Equal("Example Trust", value);
    }

    [Fact]
    public void GetDisplayValue_ReadsWrappedValueObject()
    {
        var body = """{"incomingTrustName":{"value":"Wrapped Trust","completed":true}}""";

        var formData = DashboardAnswerReader.ParseFormData(body);
        var value = DashboardAnswerReader.GetDisplayValue("incomingTrustName", formData);

        Assert.Equal("Wrapped Trust", value);
    }

    [Fact]
    public void ParseFormData_AcceptsBase64Body()
    {
        var json = """{"proposedTransferDate":"2026-03-01"}""";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        var formData = DashboardAnswerReader.ParseFormData(encoded);
        var value = DashboardAnswerReader.GetDisplayValue("proposedTransferDate", formData);

        Assert.Equal("1 March 2026", value);
    }

    [Fact]
    public void GetDisplayValue_ReturnsEmpty_WhenFieldMissing()
    {
        var formData = DashboardAnswerReader.ParseFormData("{}");
        var value = DashboardAnswerReader.GetDisplayValue("missing", formData);

        Assert.Equal(string.Empty, value);
    }

    [Fact]
    public void GetDisplayValue_ReadsFieldNestedInCollectionFlowItem()
    {
        var trustJson = """{"name":"Alpha Trust","ukprn":"12345678"}""";
        var body = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["detailsOfIncomingTrust"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["id"] = "item-1",
                    ["incomingTrustsSearch-field-flow"] = trustJson,
                    ["incomingTrustTypeOfTrust"] = "Multi-academy trust"
                }
            }
        });

        var formData = DashboardAnswerReader.ParseFormData(body);
        var trustName = DashboardAnswerReader.GetDisplayValue("incomingTrustsSearch-field-flow", formData);
        var trustType = DashboardAnswerReader.GetDisplayValue("incomingTrustTypeOfTrust", formData);

        Assert.Equal("Alpha Trust", trustName);
        Assert.Equal("Multi-academy trust", trustType);
    }

    [Fact]
    public void GetDisplayValue_SupportsDottedPathOnCollectionNestedComplexField()
    {
        var trustJson = """{"name":"Beta Trust","ukprn":"87654321"}""";
        var body = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["detailsOfIncomingTrust"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["id"] = "item-1",
                    ["incomingTrustsSearch-field-flow"] = trustJson
                }
            }
        });

        var formData = DashboardAnswerReader.ParseFormData(body);
        var value = DashboardAnswerReader.GetDisplayValue(
            "incomingTrustsSearch-field-flow.name",
            formData);

        Assert.Equal("Beta Trust", value);
    }

    [Fact]
    public void GetDisplayValue_JoinsValuesFromMultipleCollectionItems()
    {
        var body = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["detailsOfIncomingTrust"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["id"] = "1",
                    ["incomingTrustsSearch-field-flow"] = """{"name":"One Trust"}"""
                },
                new Dictionary<string, object>
                {
                    ["id"] = "2",
                    ["incomingTrustsSearch-field-flow"] = """{"name":"Two Trust"}"""
                }
            }
        });

        var formData = DashboardAnswerReader.ParseFormData(body);
        var value = DashboardAnswerReader.GetDisplayValue("incomingTrustsSearch-field-flow", formData);

        Assert.Equal("One Trust, Two Trust", value);
    }

    [Fact]
    public void GetDisplayValue_SupportsExplicitCollectionThenFieldPath()
    {
        var body = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["detailsOfIncomingTrust"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["id"] = "1",
                    ["incomingTrustAccountingOfficerFullName"] = "Alex Officer"
                }
            }
        });

        var formData = DashboardAnswerReader.ParseFormData(body);
        var value = DashboardAnswerReader.GetDisplayValue(
            "detailsOfIncomingTrust.incomingTrustAccountingOfficerFullName",
            formData);

        Assert.Equal("Alex Officer", value);
    }

    [Fact]
    public void GetDisplayValue_ReadsNameFromHtmlEncodedComplexJson()
    {
        var encodedTrust =
            "{&quot;name&quot;:&quot;BARNSBURY PRIMARY SCHOOL AND NURSERY&quot;,&quot;ukprn&quot;:&quot;10060685&quot;}";
        var body = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["detailsOfIncomingTrust"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["id"] = "item-1",
                    ["incomingTrustsSearch-field-flow"] = encodedTrust
                }
            }
        });

        var formData = DashboardAnswerReader.ParseFormData(body);

        Assert.Equal(
            "BARNSBURY PRIMARY SCHOOL AND NURSERY",
            DashboardAnswerReader.GetDisplayValue("incomingTrustsSearch-field-flow", formData));
        Assert.Equal(
            "BARNSBURY PRIMARY SCHOOL AND NURSERY",
            DashboardAnswerReader.GetDisplayValue("incomingTrustsSearch-field-flow.name", formData));
        Assert.Equal(
            "10060685",
            DashboardAnswerReader.GetDisplayValue("incomingTrustsSearch-field-flow.ukprn", formData));
    }

    [Fact]
    public void ParseFormData_ReturnsEmpty_ForNullBlankOrInvalidJson()
    {
        Assert.Empty(DashboardAnswerReader.ParseFormData(null));
        Assert.Empty(DashboardAnswerReader.ParseFormData("   "));
        Assert.Empty(DashboardAnswerReader.ParseFormData("not-json"));
    }

    [Fact]
    public void ParseFormData_SkipsTaskStatusFields()
    {
        var body = """{"TaskStatus_t1":"Completed","name":"Ada"}""";
        var formData = DashboardAnswerReader.ParseFormData(body);

        Assert.False(formData.ContainsKey("TaskStatus_t1"));
        Assert.Equal("Ada", DashboardAnswerReader.GetDisplayValue("name", formData));
    }

    [Fact]
    public void GetDisplayValue_FormatsBooleanAndPlaceholderValues()
    {
        var body = """{"isLead":true,"missing":"undefined","empty":"null"}""";
        var formData = DashboardAnswerReader.ParseFormData(body);

        Assert.Equal("Yes", DashboardAnswerReader.GetDisplayValue("isLead", formData));
        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue("missing", formData));
        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue("empty", formData));
    }

    [Fact]
    public void GetDisplayValue_FormatsIsoAndSlashDates()
    {
        var formData = DashboardAnswerReader.ParseFormData("""{"startDate":"2026-03-01","endDate":"01/04/2026"}""");

        Assert.Equal("1 March 2026", DashboardAnswerReader.GetDisplayValue("startDate", formData));
        Assert.Equal("1 April 2026", DashboardAnswerReader.GetDisplayValue("endDate", formData));
    }

    [Fact]
    public void GetDisplayValue_FormatsJsonArrayValues()
    {
        var body = """{"tags":["Alpha","Beta"]}""";
        var formData = DashboardAnswerReader.ParseFormData(body);

        Assert.Equal("Alpha, Beta", DashboardAnswerReader.GetDisplayValue("tags", formData));
    }

    [Fact]
    public void GetDisplayValue_ReadsExactDottedKeyMatch()
    {
        var body = """{"field.name":"Exact value"}""";
        var formData = DashboardAnswerReader.ParseFormData(body);

        Assert.Equal("Exact value", DashboardAnswerReader.GetDisplayValue("field.name", formData));
    }

    [Fact]
    public void GetDisplayValue_FormatsCollectionItemsByPreferredTitleFields()
    {
        var body = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["organisations"] = new[]
            {
                new Dictionary<string, object> { ["id"] = "1", ["title"] = "First Org" },
                new Dictionary<string, object> { ["id"] = "2", ["label"] = "Second Org" }
            }
        });

        var formData = DashboardAnswerReader.ParseFormData(body);
        var value = DashboardAnswerReader.GetDisplayValue("organisations", formData);

        Assert.Equal("First Org, Second Org", value);
    }

    [Fact]
    public void GetDisplayValue_ReturnsEmpty_WhenCollectionItemFieldIsMissing()
    {
        var body = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["detailsOfIncomingTrust"] = new[]
            {
                new Dictionary<string, object> { ["id"] = "1" }
            }
        });

        var formData = DashboardAnswerReader.ParseFormData(body);
        var value = DashboardAnswerReader.GetDisplayValue("missingField", formData);

        Assert.Equal(string.Empty, value);
    }

    [Fact]
    public void GetDisplayValue_ReadsNestedPropertyFromTopLevelJsonObject()
    {
        var body = """{"trust":{"name":"Gamma Trust","ukprn":"11111111"}}""";
        var formData = DashboardAnswerReader.ParseFormData(body);

        Assert.Equal("Gamma Trust", DashboardAnswerReader.GetDisplayValue("trust.name", formData));
    }

    [Fact]
    public void GetDisplayValue_FormatsFalseBooleansNumbersAndUnpaddedDates()
    {
        var formData = DashboardAnswerReader.ParseFormData(
            """{"isLead":false,"count":12,"when":"1/4/2026","emptyDate":""}""");

        Assert.Equal("No", DashboardAnswerReader.GetDisplayValue("isLead", formData));
        Assert.Equal("12", DashboardAnswerReader.GetDisplayValue("count", formData));
        Assert.Equal("1 April 2026", DashboardAnswerReader.GetDisplayValue("when", formData));
        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue("emptyDate", formData));
    }

    [Fact]
    public void GetDisplayValue_ReadsCollectionFromJsonStringAndPrefersValueProperty()
    {
        var body = """
            {
              "orgs":"[{\"id\":\"1\",\"name\":\"First\"},{\"id\":\"2\",\"title\":\"Second\"}]",
              "choice":{"value":"Selected","completed":true}
            }
            """;
        var formData = DashboardAnswerReader.ParseFormData(body);

        Assert.Equal("First, Second", DashboardAnswerReader.GetDisplayValue("orgs", formData));
        Assert.Equal("Selected", DashboardAnswerReader.GetDisplayValue("choice", formData));
    }

    [Fact]
    public void GetDisplayValue_UsesLabelFromComplexJsonWithoutValueWrapper()
    {
        var formData = DashboardAnswerReader.ParseFormData(
            """{"item":{"label":"Shown label"},"plain":{"ukprn":"123"}}""");

        Assert.Equal("Shown label", DashboardAnswerReader.GetDisplayValue("item", formData));
        Assert.Contains("ukprn", DashboardAnswerReader.GetDisplayValue("plain", formData));
    }
}
