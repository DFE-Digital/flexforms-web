using System.Text.Json;
using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Domain.Tests.Models;

public class ComplexFieldSerializationTests
{
    [Fact]
    public void ValidationStatusText_round_trips()
    {
        const string json = """{"id":"Upload","validationStatusText":{"pending":"P","passed":"OK","failed":"No"}}""";

        var field = JsonSerializer.Deserialize<ComplexField>(json);

        Assert.NotNull(field?.ValidationStatusText);
        Assert.Equal("P", field.ValidationStatusText.Pending);
        Assert.Equal("OK", field.ValidationStatusText.Passed);
        Assert.Equal("No", field.ValidationStatusText.Failed);

        var written = JsonSerializer.Serialize(field);
        Assert.Contains("\"validationStatusText\"", written);
        Assert.Contains("\"pending\":\"P\"", written);
        Assert.Contains("\"passed\":\"OK\"", written);
        Assert.Contains("\"failed\":\"No\"", written);
    }

    [Fact]
    public void ValidationStatusText_is_null_when_missing()
    {
        var field = JsonSerializer.Deserialize<ComplexField>("""{"id":"Upload"}""");

        Assert.NotNull(field);
        Assert.Null(field.ValidationStatusText);
    }
}
