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

    [Fact]
    public void GetDisplayValue_ReturnsEmpty_ForNullOrWhitespaceFieldPath()
    {
        var formData = DashboardAnswerReader.ParseFormData("""{"name":"Ada"}""");

        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue(null!, formData));
        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue("   ", formData));
    }

    [Fact]
    public void GetDisplayValue_ReturnsEmpty_WhenFieldPathIsOnlySeparators()
    {
        var formData = DashboardAnswerReader.ParseFormData("""{"name":"Ada"}""");

        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue("...", formData));
    }

    [Fact]
    public void ParseFormData_ReturnsEmpty_WhenBase64DecodesToWhitespace()
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("   "));

        Assert.Empty(DashboardAnswerReader.ParseFormData(encoded));
    }

    [Fact]
    public void ParseFormData_ReturnsEmpty_WhenJsonIsNullLiteral()
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("null"));

        Assert.Empty(DashboardAnswerReader.ParseFormData(encoded));
    }

    [Fact]
    public void ParseFormData_MapsJsonNullAndFractionalNumbers()
    {
        var formData = DashboardAnswerReader.ParseFormData("""{"nothing":null,"ratio":1.5}""");

        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue("nothing", formData));
        Assert.Equal("1.5", DashboardAnswerReader.GetDisplayValue("ratio", formData));
    }

    [Fact]
    public void GetDisplayValue_SkipsBlankValuesWhenJoiningCollectionItems()
    {
        var body = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["items"] = new[]
            {
                new Dictionary<string, object> { ["id"] = "1", ["note"] = "   " },
                new Dictionary<string, object> { ["id"] = "2", ["note"] = "Kept" }
            }
        });

        var formData = DashboardAnswerReader.ParseFormData(body);

        Assert.Equal("Kept", DashboardAnswerReader.GetDisplayValue("note", formData));
        Assert.Equal("Kept", DashboardAnswerReader.GetDisplayValue("items.note", formData));
    }

    [Fact]
    public void GetDisplayValue_ReturnsEmpty_WhenCollectionItemsHaveNoPreferredTitleField()
    {
        var body = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["orgs"] = new[]
            {
                new Dictionary<string, object> { ["id"] = "1", ["ukprn"] = "12345678" }
            }
        });

        var formData = DashboardAnswerReader.ParseFormData(body);

        // A trailing separator resolves the collection itself rather than an exact key.
        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue("orgs.", formData));
    }

    [Fact]
    public void GetDisplayValue_SkipsPlaceholderNameAndFallsBackToTitle()
    {
        var body = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["orgs"] = new[]
            {
                new Dictionary<string, object> { ["name"] = "undefined", ["title"] = "Fallback Org" },
                new Dictionary<string, object> { ["label"] = "Second Org" }
            }
        });

        var formData = DashboardAnswerReader.ParseFormData(body);

        Assert.Equal("Fallback Org, Second Org", DashboardAnswerReader.GetDisplayValue("orgs.", formData));
    }

    [Fact]
    public void GetDisplayValue_ReturnsEmpty_WhenCollectionJsonStringIsMalformed()
    {
        var body = """{"orgs":"[{\"name\":\"Broken\""}""";

        var formData = DashboardAnswerReader.ParseFormData(body);

        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue("name", formData));
    }

    [Fact]
    public void GetDisplayValue_ReturnsEmpty_ForEmptyCollectionJsonString()
    {
        var formData = DashboardAnswerReader.ParseFormData("""{"orgs":"[]"}""");

        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue("orgs.name", formData));
    }

    [Fact]
    public void GetDisplayValue_IgnoresScalarAndNonCollectionValuesWhenSearchingCollections()
    {
        var formData = DashboardAnswerReader.ParseFormData(
            """{"flag":true,"count":5,"tags":["a","b"],"note":"plain text"}""");

        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue("missing", formData));
    }

    [Fact]
    public void GetDisplayValue_ReadsCollectionFromJsonElementArray()
    {
        using var doc = JsonDocument.Parse("""[{"name":"Element Org"},{"name":"Second Org"}]""");
        var formData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["orgs"] = doc.RootElement.Clone()
        };

        Assert.Equal("Element Org, Second Org", DashboardAnswerReader.GetDisplayValue("orgs.name", formData));
        Assert.Equal("Element Org, Second Org", DashboardAnswerReader.GetDisplayValue("orgs", formData));
    }

    [Fact]
    public void GetDisplayValue_ReadsCollectionFromJsonElementString()
    {
        using var doc = JsonDocument.Parse("""{"orgs":"[{\"name\":\"String Org\"}]"}""");
        var formData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["orgs"] = doc.RootElement.GetProperty("orgs").Clone()
        };

        Assert.Equal("String Org", DashboardAnswerReader.GetDisplayValue("orgs.name", formData));
    }

    [Fact]
    public void GetDisplayValue_ReturnsEmpty_WhenJsonElementValueIsNotACollection()
    {
        using var doc = JsonDocument.Parse("""{"count":5}""");
        var formData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["count"] = doc.RootElement.GetProperty("count").Clone()
        };

        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue("missing", formData));
    }

    [Fact]
    public void GetDisplayValue_ParsesCollectionItemsFromMixedObjectList()
    {
        using var doc = JsonDocument.Parse(
            """{"obj":{"name":"Element Item"},"str":"{\"name\":\"String Element Item\"}","num":7}""");
        var root = doc.RootElement;
        var formData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["items"] = new List<object>
            {
                new Dictionary<string, object> { ["name"] = "Dictionary Item" },
                root.GetProperty("obj").Clone(),
                root.GetProperty("str").Clone(),
                root.GetProperty("num").Clone(),
                """{"name":"Raw String Item"}""",
                "not-json",
                "{broken",
                null!
            }
        };

        Assert.Equal(
            "Dictionary Item, Element Item, String Element Item, Raw String Item",
            DashboardAnswerReader.GetDisplayValue("items.name", formData));
    }

    [Fact]
    public void GetDisplayValue_ReadsNestedPropertyFromDictionaryValue()
    {
        var formData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["trust"] = new Dictionary<string, object>
            {
                ["address"] = new Dictionary<string, object> { ["postcode"] = "SW1A 1AA" }
            }
        };

        Assert.Equal("SW1A 1AA", DashboardAnswerReader.GetDisplayValue("trust.address.postcode", formData));
        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue("trust.address.missing", formData));
    }

    [Fact]
    public void GetDisplayValue_ReadsNestedPropertyFromJsonElementValues()
    {
        using var doc = JsonDocument.Parse(
            """{"obj":{"name":"Object Trust"},"str":"{\"name\":\"String Trust\"}","num":9}""");
        var root = doc.RootElement;
        var formData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["objTrust"] = root.GetProperty("obj").Clone(),
            ["strTrust"] = root.GetProperty("str").Clone(),
            ["numTrust"] = root.GetProperty("num").Clone()
        };

        Assert.Equal("Object Trust", DashboardAnswerReader.GetDisplayValue("objTrust.name", formData));
        Assert.Equal("String Trust", DashboardAnswerReader.GetDisplayValue("strTrust.name", formData));
        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue("numTrust.name", formData));
    }

    [Fact]
    public void GetDisplayValue_ReturnsEmpty_ForNestedPathOnBlankMalformedOrArrayValues()
    {
        var formData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["blank"] = "   ",
            ["broken"] = "{\"name\":",
            ["scalars"] = "[1,2]"
        };

        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue("blank.name", formData));
        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue("broken.name", formData));
        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue("scalars.name", formData));
    }

    [Fact]
    public void GetDisplayValue_ReturnsEmpty_ForNestedPathOnNumericValue()
    {
        var formData = DashboardAnswerReader.ParseFormData("""{"count":12}""");

        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue("count.name", formData));
    }

    [Fact]
    public void GetDisplayValue_DecodesDoubleEncodedComplexJson()
    {
        var formData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["quoted"] = "{&amp;quot;name&amp;quot;:&amp;quot;Double Quoted Trust&amp;quot;}",
            ["numeric"] = "{&amp;#34;name&amp;#34;:&amp;#34;Numeric Entity Trust&amp;#34;}"
        };

        Assert.Equal("Double Quoted Trust", DashboardAnswerReader.GetDisplayValue("quoted", formData));
        Assert.Equal("Numeric Entity Trust", DashboardAnswerReader.GetDisplayValue("numeric", formData));
    }

    [Fact]
    public void GetDisplayValue_PrefersTextThenValuePropertiesFromComplexJson()
    {
        var formData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["withText"] = """{"text":"Text prop"}""",
            ["withValue"] = """{"value":"Value prop"}""",
            ["skipsNonStrings"] = """{"name":123,"title":"   ","label":"Label prop"}"""
        };

        Assert.Equal("Text prop", DashboardAnswerReader.GetDisplayValue("withText", formData));
        Assert.Equal("Value prop", DashboardAnswerReader.GetDisplayValue("withValue", formData));
        Assert.Equal("Label prop", DashboardAnswerReader.GetDisplayValue("skipsNonStrings", formData));
    }

    [Fact]
    public void GetDisplayValue_FallsBackToRawText_WhenComplexJsonIsMalformed()
    {
        var raw = "{\"name\":\"Unclosed";
        var formData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["broken"] = raw
        };

        Assert.Equal(raw, DashboardAnswerReader.GetDisplayValue("broken", formData));
    }

    [Fact]
    public void GetDisplayValue_UnwrapsValueObjectsInsideCollectionItems()
    {
        var body = """{"orgs":"[{\"id\":\"1\",\"orgName\":{\"value\":\"Wrapped Org\",\"completed\":true}}]"}""";

        var formData = DashboardAnswerReader.ParseFormData(body);

        Assert.Equal("Wrapped Org", DashboardAnswerReader.GetDisplayValue("orgName", formData));
    }

    [Fact]
    public void GetDisplayValue_SupportsDeepPropertyPathInsideCollectionItems()
    {
        var body = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["orgs"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["id"] = "1",
                    ["trust"] = new Dictionary<string, object>
                    {
                        ["address"] = new Dictionary<string, object> { ["postcode"] = "AB1 2CD" }
                    }
                }
            }
        });

        var formData = DashboardAnswerReader.ParseFormData(body);

        Assert.Equal("AB1 2CD", DashboardAnswerReader.GetDisplayValue("orgs.trust.address.postcode", formData));
    }
}
