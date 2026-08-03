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
}
