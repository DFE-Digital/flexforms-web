using GovUK.Dfe.FlexForms.Domain.Templates;
using GovUK.Dfe.FlexForms.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Services;

public class TemplateValidationServiceTests
{
    private readonly TemplateValidationService _service = new(NullLogger<TemplateValidationService>.Instance);

    [Fact]
    public void ValidateTemplateJson_ShouldFail_WhenEmpty()
    {
        var (isValid, errors) = _service.ValidateTemplateJson(" ");

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("required"));
    }

    [Fact]
    public void ValidateTemplateJson_ShouldFail_WhenJsonIsInvalid()
    {
        var (isValid, errors) = _service.ValidateTemplateJson("{not-json");

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("JSON parsing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateTemplateJson_ShouldPass_ForStarterSchema()
    {
        var json = StarterFormTemplateSchema.CreateJson(Guid.NewGuid().ToString(), "Starter");

        var (isValid, errors) = _service.ValidateTemplateJson(json);

        Assert.True(isValid, string.Join("; ", errors));
        var (template, parseErrors) = _service.TryParseTemplate(json);
        Assert.NotNull(template);
        Assert.Empty(parseErrors);
    }
}
