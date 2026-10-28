using System.Text;
using GovUK.Dfe.FlexForms.Web.Pages.Admin;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages.Admin;

public class TemplateStudioSchemaTransportTests
{
    [Fact]
    public void Decode_ShouldDecodePrefixedBase64_IncludingNonAsciiText()
    {
        const string schema = """{ "templateName": "Café – trust's <form>" }""";
        var encoded = TemplateStudioSchemaTransport.Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(schema));

        Assert.Equal(schema, TemplateStudioSchemaTransport.Decode(encoded));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("""{ "templateId": "x" }""")]
    [InlineData("b64:not base64!")]
    public void Decode_ShouldReturnOtherValuesUnchanged(string? value)
    {
        Assert.Equal(value, TemplateStudioSchemaTransport.Decode(value));
    }
}
