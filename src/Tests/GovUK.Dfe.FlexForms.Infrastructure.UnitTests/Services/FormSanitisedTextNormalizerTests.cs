using GovUK.Dfe.FlexForms.Infrastructure.Services;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Services;

public class FormSanitisedTextNormalizerTests
{
    [Fact]
    public void ToPlainTextForCharacterCountValidation_replaces_br_tags_with_newlines_then_decodes_entities()
    {
        const string sanitised = "Some<br>new<br/>lines<br />here";

        var result = FormSanitisedTextNormalizer.ToPlainTextForCharacterCountValidation(sanitised);

        Assert.Equal("Some\nnew\nlines\nhere", result);
    }

    [Fact]
    public void ToPlainTextForCharacterCountValidation_returns_empty_unchanged()
    {
        Assert.Equal(string.Empty, FormSanitisedTextNormalizer.ToPlainTextForCharacterCountValidation(string.Empty));
    }
}
