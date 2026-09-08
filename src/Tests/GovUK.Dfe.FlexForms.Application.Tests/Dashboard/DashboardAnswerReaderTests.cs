using GovUK.Dfe.FlexForms.Application.Dashboard;
using System.Text;
using System.Text.Json;

namespace GovUK.Dfe.FlexForms.Application.Tests.Dashboard;

public class DashboardAnswerReaderTests
{
    // ---------------------------------------------------------------------
    // ParseFormData
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseFormData_ShouldReturnEmpty_WhenResponseBodyIsBlank(string? responseBody) =>
        Assert.Empty(DashboardAnswerReader.ParseFormData(responseBody));

    [Fact]
    public void ParseFormData_ShouldDecodeBase64Body()
    {
        var formData = DashboardAnswerReader.ParseFormData(Base64("""{ "academyName": "Oakwood" }"""));

        Assert.Equal("Oakwood", formData["academyName"]);
    }

    [Fact]
    public void ParseFormData_ShouldReadPlainJson_WhenBodyIsNotBase64()
    {
        var formData = DashboardAnswerReader.ParseFormData("""{ "academyName": "Oakwood" }""");

        Assert.Equal("Oakwood", formData["academyName"]);
    }

    [Fact]
    public void ParseFormData_ShouldMatchKeysCaseInsensitively()
    {
        var formData = DashboardAnswerReader.ParseFormData("""{ "academyName": "Oakwood" }""");

        Assert.Equal("Oakwood", formData["ACADEMYNAME"]);
    }

    [Fact]
    public void ParseFormData_ShouldReturnEmpty_WhenDecodedBodyIsBlank() =>
        Assert.Empty(DashboardAnswerReader.ParseFormData(Base64("   ")));

    [Fact]
    public void ParseFormData_ShouldReturnEmpty_WhenJsonIsNullLiteral() =>
        Assert.Empty(DashboardAnswerReader.ParseFormData(Base64("null")));

    [Fact]
    public void ParseFormData_ShouldReturnEmpty_WhenJsonHasNoFields() =>
        Assert.Empty(DashboardAnswerReader.ParseFormData(Base64("{}")));

    [Fact]
    public void ParseFormData_ShouldReturnEmpty_WhenJsonIsMalformed() =>
        Assert.Empty(DashboardAnswerReader.ParseFormData(Base64("{ not json")));

    [Fact]
    public void ParseFormData_ShouldSkipTaskStatusKeys()
    {
        var formData = DashboardAnswerReader.ParseFormData(
            """{ "TaskStatus_details": "Completed", "academyName": "Oakwood" }""");

        Assert.Equal("academyName", Assert.Single(formData.Keys));
    }

    [Fact]
    public void ParseFormData_ShouldUnwrapValueWrappedFields()
    {
        var formData = DashboardAnswerReader.ParseFormData(
            """{ "academyName": { "value": "Oakwood", "capturedOn": "2024-01-01" } }""");

        Assert.Equal("Oakwood", formData["academyName"]);
    }

    [Fact]
    public void ParseFormData_ShouldKeepRawJson_WhenObjectHasNoValueProperty()
    {
        var formData = DashboardAnswerReader.ParseFormData("""{ "trust": { "name": "Oak Trust" } }""");

        Assert.Equal("""{ "name": "Oak Trust" }""", formData["trust"]);
    }

    [Fact]
    public void ParseFormData_ShouldConvertJsonScalarsToClrValues()
    {
        var formData = DashboardAnswerReader.ParseFormData("""
            {
              "count": 42,
              "ratio": 1.5,
              "agreed": true,
              "declined": false,
              "missing": null,
              "tags": [1, "two"]
            }
            """);

        // Numbers are widened to double by the reader's ternary, whole or not.
        Assert.Equal(42d, Assert.IsType<double>(formData["count"]));
        Assert.Equal(1.5d, Assert.IsType<double>(formData["ratio"]));
        Assert.True(Assert.IsType<bool>(formData["agreed"]));
        Assert.False(Assert.IsType<bool>(formData["declined"]));
        Assert.Equal(string.Empty, Assert.IsType<string>(formData["missing"]));
        Assert.Equal(new List<object> { 1d, "two" }, Assert.IsType<List<object>>(formData["tags"]));
    }

    // ---------------------------------------------------------------------
    // GetDisplayValue - guard clauses and exact matches
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetDisplayValue_ShouldReturnEmpty_WhenFieldPathIsBlank(string? fieldPath) =>
        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue(fieldPath!, Data(("a", "b"))));

    [Fact]
    public void GetDisplayValue_ShouldReturnEmpty_WhenFieldPathHasNoUsableSegments() =>
        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue("..", Data(("a", "b"))));

    [Fact]
    public void GetDisplayValue_ShouldUseExactKey_WhenKeyItselfContainsDots()
    {
        var formData = Data(("trust.name", "Oak Trust"));

        Assert.Equal("Oak Trust", DashboardAnswerReader.GetDisplayValue("trust.name", formData));
    }

    [Fact]
    public void GetDisplayValue_ShouldReturnEmpty_WhenFieldIsUnknown() =>
        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue("missing", Data(("a", "b"))));

    [Fact]
    public void GetDisplayValue_ShouldReturnEmpty_WhenTopLevelValueIsNull() =>
        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue("academyName", Data(("academyName", null!))));

    // ---------------------------------------------------------------------
    // GetDisplayValue - scalar formatting
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(true, "Yes")]
    [InlineData(false, "No")]
    public void GetDisplayValue_ShouldFormatBooleansAsYesOrNo(bool value, string expected) =>
        Assert.Equal(expected, DashboardAnswerReader.GetDisplayValue("agreed", Data(("agreed", value))));

    [Theory]
    [InlineData("2024-03-15", "15 March 2024")]
    [InlineData("15/03/2024", "15 March 2024")]
    [InlineData("5/3/2024", "5 March 2024")]
    public void GetDisplayValue_ShouldFormatRecognisedDates(string stored, string expected) =>
        Assert.Equal(expected, DashboardAnswerReader.GetDisplayValue("startDate", Data(("startDate", stored))));

    [Fact]
    public void GetDisplayValue_ShouldReturnTextUnchanged_WhenHyphenatedValueIsNotADate() =>
        Assert.Equal(
            "multi-academy trust",
            DashboardAnswerReader.GetDisplayValue("trustType", Data(("trustType", "multi-academy trust"))));

    [Theory]
    [InlineData("undefined")]
    [InlineData("NULL")]
    [InlineData("   ")]
    public void GetDisplayValue_ShouldReturnEmpty_ForPlaceholderOrBlankValues(string stored) =>
        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue("academyName", Data(("academyName", stored))));

    [Fact]
    public void GetDisplayValue_ShouldTrimSurroundingWhitespace() =>
        Assert.Equal("Oakwood", DashboardAnswerReader.GetDisplayValue("academyName", Data(("academyName", "  Oakwood  "))));

    [Fact]
    public void GetDisplayValue_ShouldFormatNumbersAsText() =>
        Assert.Equal("42", DashboardAnswerReader.GetDisplayValue("count", Data(("count", 42L))));

    [Fact]
    public void GetDisplayValue_ShouldFormatJsonElementValues() =>
        Assert.Equal("42", DashboardAnswerReader.GetDisplayValue("count", Data(("count", Element("42")))));

    [Fact]
    public void GetDisplayValue_ShouldJoinListValues_AndDropBlankEntries()
    {
        var formData = Data(("tags", new List<object> { "alpha", true, "   " }));

        Assert.Equal("alpha, Yes", DashboardAnswerReader.GetDisplayValue("tags", formData));
    }

    // ---------------------------------------------------------------------
    // GetDisplayValue - complex JSON scalars
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("""{ "name": "Oak Trust" }""")]
    [InlineData("""{ "title": "Oak Trust" }""")]
    [InlineData("""{ "label": "Oak Trust" }""")]
    [InlineData("""{ "text": "Oak Trust" }""")]
    [InlineData("""{ "value": "Oak Trust" }""")]
    public void GetDisplayValue_ShouldPreferReadablePropertyFromComplexJson(string stored) =>
        Assert.Equal("Oak Trust", DashboardAnswerReader.GetDisplayValue("trust", Data(("trust", stored))));

    [Fact]
    public void GetDisplayValue_ShouldReturnRawJson_WhenComplexValueHasNoReadableProperty()
    {
        const string stored = """{"ukprn":"10012345"}""";

        Assert.Equal(stored, DashboardAnswerReader.GetDisplayValue("trust", Data(("trust", stored))));
    }

    [Fact]
    public void GetDisplayValue_ShouldIgnoreNonStringReadableProperty()
    {
        const string stored = """{"name":123}""";

        Assert.Equal(stored, DashboardAnswerReader.GetDisplayValue("trust", Data(("trust", stored))));
    }

    [Fact]
    public void GetDisplayValue_ShouldJoinJsonArrayValues() =>
        Assert.Equal(
            "alpha, beta",
            DashboardAnswerReader.GetDisplayValue("tags", Data(("tags", """["alpha","  ","beta"]"""))));

    [Fact]
    public void GetDisplayValue_ShouldReturnRawText_WhenComplexJsonIsMalformed() =>
        Assert.Equal("{ broken", DashboardAnswerReader.GetDisplayValue("trust", Data(("trust", "{ broken"))));

    [Fact]
    public void GetDisplayValue_ShouldDecodeHtmlEncodedJson() =>
        Assert.Equal(
            "Oak Trust",
            DashboardAnswerReader.GetDisplayValue(
                "trust",
                Data(("trust", "{&quot;name&quot;:&quot;Oak Trust&quot;}"))));

    [Fact]
    public void GetDisplayValue_ShouldDecodeDoubleHtmlEncodedJson() =>
        Assert.Equal(
            "Oak Trust",
            DashboardAnswerReader.GetDisplayValue(
                "trust",
                Data(("trust", "{&amp;quot;name&amp;quot;:&amp;quot;Oak Trust&amp;quot;}"))));

    [Fact]
    public void GetDisplayValue_ShouldDecodeNumericHtmlEntities() =>
        Assert.Equal(
            "Oak Trust",
            DashboardAnswerReader.GetDisplayValue(
                "trust",
                Data(("trust", "{&amp;#34;name&amp;#34;:&amp;#34;Oak Trust&amp;#34;}"))));

    // ---------------------------------------------------------------------
    // GetDisplayValue - dotted property paths
    // ---------------------------------------------------------------------

    [Fact]
    public void GetDisplayValue_ShouldReadNestedPropertyFromJsonString() =>
        Assert.Equal(
            "Oak Trust",
            DashboardAnswerReader.GetDisplayValue("trust.name", Data(("trust", """{"name":"Oak Trust"}"""))));

    [Fact]
    public void GetDisplayValue_ShouldReadNestedPropertyFromJsonElementObject() =>
        Assert.Equal(
            "Oak Trust",
            DashboardAnswerReader.GetDisplayValue("trust.name", Data(("trust", Element("""{"name":"Oak Trust"}""")))));

    [Fact]
    public void GetDisplayValue_ShouldReadNestedPropertyFromJsonElementString() =>
        Assert.Equal(
            "Oak Trust",
            DashboardAnswerReader.GetDisplayValue(
                "trust.name",
                Data(("trust", StringElement("""{"name":"Oak Trust"}""")))));

    [Fact]
    public void GetDisplayValue_ShouldReadNestedPropertyFromDictionary()
    {
        var formData = Data(("trust", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = "Oak Trust"
        }));

        Assert.Equal("Oak Trust", DashboardAnswerReader.GetDisplayValue("trust.name", formData));
    }

    [Fact]
    public void GetDisplayValue_ShouldWalkMultipleNestedSegments()
    {
        var formData = Data(("trust", """{"address":{"town":"Leeds"}}"""));

        Assert.Equal("Leeds", DashboardAnswerReader.GetDisplayValue("trust.address.town", formData));
    }

    [Fact]
    public void GetDisplayValue_ShouldReturnEmpty_WhenNestedPropertyIsMissing() =>
        Assert.Equal(
            string.Empty,
            DashboardAnswerReader.GetDisplayValue("trust.name", Data(("trust", """{"ukprn":"10012345"}"""))));

    [Fact]
    public void GetDisplayValue_ShouldReturnEmpty_WhenNestedValueIsNotJson() =>
        Assert.Equal(
            string.Empty,
            DashboardAnswerReader.GetDisplayValue("trust.name", Data(("trust", "Oak Trust"))));

    [Fact]
    public void GetDisplayValue_ShouldReturnEmpty_WhenNestedValueIsBlank() =>
        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue("trust.name", Data(("trust", "   "))));

    [Fact]
    public void GetDisplayValue_ShouldReturnEmpty_WhenNestedValueIsMalformedJson() =>
        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue("trust.name", Data(("trust", "{ broken"))));

    [Fact]
    public void GetDisplayValue_ShouldReturnEmpty_WhenNestedLookupTargetsANonJsonType() =>
        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue("count.name", Data(("count", 42L))));

    [Fact]
    public void GetDisplayValue_ShouldReturnEmpty_WhenNestedLookupTargetsANonStringJsonElement() =>
        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue("count.name", Data(("count", Element("42")))));

    // ---------------------------------------------------------------------
    // GetDisplayValue - collection flows
    // ---------------------------------------------------------------------

    [Fact]
    public void GetDisplayValue_ShouldJoinFieldFromCollectionItems()
    {
        var formData = Data(("trusts-field-flow", """[{"name":"Oak Trust"},{"name":"Elm Trust"}]"""));

        Assert.Equal("Oak Trust, Elm Trust", DashboardAnswerReader.GetDisplayValue("trusts-field-flow.name", formData));
    }

    [Fact]
    public void GetDisplayValue_ShouldSkipCollectionItemsWithoutTheField()
    {
        var formData = Data(("trusts-field-flow", """[{"name":"Oak Trust"},{"ukprn":"1"},{"name":"   "}]"""));

        Assert.Equal("Oak Trust", DashboardAnswerReader.GetDisplayValue("trusts-field-flow.name", formData));
    }

    [Fact]
    public void GetDisplayValue_ShouldReturnEmpty_WhenCollectionItemFieldIsNull()
    {
        var formData = Data(("trusts-field-flow", new List<object>
        {
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["name"] = null! }
        }));

        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue("trusts-field-flow.name", formData));
    }

    [Fact]
    public void GetDisplayValue_ShouldUnwrapValueWrappedPropertiesInCollectionItems()
    {
        var formData = Data(("trusts-field-flow", """[{"name":{"value":"Oak Trust"}}]"""));

        Assert.Equal("Oak Trust", DashboardAnswerReader.GetDisplayValue("trusts-field-flow.name", formData));
    }

    [Fact]
    public void GetDisplayValue_ShouldResolveNestedPathInsideCollectionItems()
    {
        var formData = Data(("trusts-field-flow", """[{"trust":{"name":"Oak Trust"}}]"""));

        Assert.Equal("Oak Trust", DashboardAnswerReader.GetDisplayValue("trusts-field-flow.trust.name", formData));
    }

    [Fact]
    public void GetDisplayValue_ShouldReturnEmpty_WhenNestedPathInsideCollectionItemIsMissing()
    {
        var formData = Data(("trusts-field-flow", """[{"trust":{"ukprn":"1"}}]"""));

        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue("trusts-field-flow.trust.name", formData));
    }

    [Fact]
    public void GetDisplayValue_ShouldSummariseCollectionItems_WhenNoNestedFieldIsRequested()
    {
        var formData = Data(("trusts-field-flow", """
            [{"name":"Oak Trust"},{"title":"Elm Trust"},{"label":"Ash Trust"},{"ukprn":"1"}]
            """));

        Assert.Equal(
            "Oak Trust, Elm Trust, Ash Trust",
            DashboardAnswerReader.GetDisplayValue("trusts-field-flow.", formData));
    }

    [Fact]
    public void GetDisplayValue_ShouldFindFieldInsideAnyCollection_WhenFieldIsNotTopLevel()
    {
        var formData = Data(
            ("trusts-field-flow", """[{"trustName":"Oak Trust"},{"trustName":"Elm Trust"}]"""),
            ("academyName", "Oakwood"));

        Assert.Equal("Oak Trust, Elm Trust", DashboardAnswerReader.GetDisplayValue("trustName", formData));
    }

    [Fact]
    public void GetDisplayValue_ShouldFindNestedPropertyInsideAnyCollection()
    {
        var formData = Data(("trusts-field-flow", """[{"trust":{"name":"Oak Trust"}},{"trust":{"ukprn":"1"}}]"""));

        Assert.Equal("Oak Trust", DashboardAnswerReader.GetDisplayValue("trust.name", formData));
    }

    [Fact]
    public void GetDisplayValue_ShouldIgnoreNullValues_WhenScanningAllCollections() =>
        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue("trustName", Data(("flow", null!))));

    [Fact]
    public void GetDisplayValue_ShouldReturnEmpty_WhenCollectionIsAnEmptyJsonArray() =>
        Assert.Equal(
            string.Empty,
            DashboardAnswerReader.GetDisplayValue("trusts-field-flow.name", Data(("trusts-field-flow", "[]"))));

    [Fact]
    public void GetDisplayValue_ShouldNotTreatScalarJsonArrayAsCollection() =>
        Assert.Equal(
            string.Empty,
            DashboardAnswerReader.GetDisplayValue("trusts-field-flow.name", Data(("trusts-field-flow", "[1,2]"))));

    [Fact]
    public void GetDisplayValue_ShouldIgnoreMalformedCollectionJson() =>
        Assert.Equal(
            string.Empty,
            DashboardAnswerReader.GetDisplayValue("trusts-field-flow.name", Data(("trusts-field-flow", "[ broken"))));

    [Fact]
    public void GetDisplayValue_ShouldReadCollectionFromJsonElementArray()
    {
        var formData = Data(("trusts-field-flow", Element("""[{"name":"Oak Trust"}]""")));

        Assert.Equal("Oak Trust", DashboardAnswerReader.GetDisplayValue("trusts-field-flow.name", formData));
    }

    [Fact]
    public void GetDisplayValue_ShouldReadCollectionFromJsonElementStringHoldingAnArray()
    {
        var formData = Data(("trusts-field-flow", StringElement("""[{"name":"Oak Trust"}]""")));

        Assert.Equal("Oak Trust", DashboardAnswerReader.GetDisplayValue("trusts-field-flow.name", formData));
    }

    [Fact]
    public void GetDisplayValue_ShouldReadCollectionFromObjectList()
    {
        var formData = Data(("trusts-field-flow", new List<object>
        {
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["name"] = "Oak Trust" }
        }));

        Assert.Equal("Oak Trust", DashboardAnswerReader.GetDisplayValue("trusts-field-flow.name", formData));
    }

    [Fact]
    public void GetDisplayValue_ShouldReadCollectionFromListOfJsonObjectStrings()
    {
        var formData = Data(("trusts-field-flow", new List<object> { """{"name":"Oak Trust"}""" }));

        Assert.Equal("Oak Trust", DashboardAnswerReader.GetDisplayValue("trusts-field-flow.name", formData));
    }

    [Fact]
    public void GetDisplayValue_ShouldReadCollectionFromListOfJsonElementObjects()
    {
        var formData = Data(("trusts-field-flow", new List<object> { Element("""{"name":"Oak Trust"}""") }));

        Assert.Equal("Oak Trust", DashboardAnswerReader.GetDisplayValue("trusts-field-flow.name", formData));
    }

    [Fact]
    public void GetDisplayValue_ShouldReadCollectionFromListOfJsonElementStrings()
    {
        var formData = Data(("trusts-field-flow", new List<object> { StringElement("""{"name":"Oak Trust"}""") }));

        Assert.Equal("Oak Trust", DashboardAnswerReader.GetDisplayValue("trusts-field-flow.name", formData));
    }

    [Fact]
    public void GetDisplayValue_ShouldIgnoreUnparseableListEntries()
    {
        var formData = Data(("trusts-field-flow", new List<object>
        {
            null!,
            42L,
            "plain text",
            "{ broken",
            Element("42"),
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["name"] = "Oak Trust" }
        }));

        Assert.Equal("Oak Trust", DashboardAnswerReader.GetDisplayValue("trusts-field-flow.name", formData));
    }

    [Fact]
    public void GetDisplayValue_ShouldNotTreatListOfScalarsAsCollection()
    {
        var formData = Data(("trusts-field-flow", new List<object> { 1L, 2L }));

        Assert.Equal(string.Empty, DashboardAnswerReader.GetDisplayValue("trusts-field-flow.name", formData));
    }

    [Fact]
    public void GetDisplayValue_ShouldFormatJsonObjectElement_WhenItIsNotACollection() =>
        Assert.Equal(
            "Oak Trust",
            DashboardAnswerReader.GetDisplayValue("trust.", Data(("trust", Element("""{"name":"Oak Trust"}""")))));

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static string Base64(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private static JsonElement Element(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static JsonElement StringElement(string value) => Element(JsonSerializer.Serialize(value));

    private static Dictionary<string, object> Data(params (string Key, object Value)[] entries)
    {
        var formData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in entries)
            formData[key] = value;

        return formData;
    }
}
