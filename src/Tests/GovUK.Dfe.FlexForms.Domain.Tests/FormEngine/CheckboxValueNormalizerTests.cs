using System.Text.Json;
using GovUK.Dfe.FlexForms.Domain.FormEngine;

namespace GovUK.Dfe.FlexForms.Domain.Tests.FormEngine;

public class CheckboxValueNormalizerTests
{
    [Fact]
    public void Normalize_ShouldReturnEmpty_WhenValueIsNullOrBlank()
    {
        Assert.Empty(CheckboxValueNormalizer.Normalize(null));
        Assert.Empty(CheckboxValueNormalizer.Normalize("   "));
    }

    [Fact]
    public void Normalize_ShouldReturnSingleString()
    {
        Assert.Equal(["yes"], CheckboxValueNormalizer.Normalize("yes"));
    }

    [Fact]
    public void Normalize_ShouldParseJsonArrayString()
    {
        var result = CheckboxValueNormalizer.Normalize("""["a","b",""]""");

        Assert.Equal(["a", "b"], result);
    }

    [Fact]
    public void Normalize_ShouldReturnRawString_WhenJsonArrayIsInvalid()
    {
        var raw = "[not-json]";
        Assert.Equal([raw], CheckboxValueNormalizer.Normalize(raw));
    }

    [Fact]
    public void Normalize_ShouldFilterStringEnumerable()
    {
        Assert.Equal(["one", "two"], CheckboxValueNormalizer.Normalize(new[] { "one", " ", "two" }));
    }

    [Fact]
    public void Normalize_ShouldHandleJsonElementArrayAndString()
    {
        using var arrayDoc = JsonDocument.Parse("""["x",1]""");
        Assert.Equal(["x", "1"], CheckboxValueNormalizer.Normalize(arrayDoc.RootElement.Clone()));

        using var stringDoc = JsonDocument.Parse("\"hello\"");
        Assert.Equal(["hello"], CheckboxValueNormalizer.Normalize(stringDoc.RootElement.Clone()));

        using var emptyStringDoc = JsonDocument.Parse("\"  \"");
        Assert.Empty(CheckboxValueNormalizer.Normalize(emptyStringDoc.RootElement.Clone()));
    }

    [Fact]
    public void Normalize_ShouldHandleObjectEnumerableAndFallback()
    {
        Assert.Equal(["1", "2"], CheckboxValueNormalizer.Normalize(new object[] { 1, 2 }));
        Assert.Equal(["42"], CheckboxValueNormalizer.Normalize(42));
    }
}
