using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Domain.Tests.Models;

public class ComplexFieldConfirmationTitleTests
{
    [Fact]
    public void Format_uses_the_default_heading_when_the_template_is_blank()
    {
        Assert.Equal("Is this the right local authority?", ComplexFieldConfirmationTitle.Format(null, "local authority"));
        Assert.Equal("Is this the right local authority?", ComplexFieldConfirmationTitle.Format("  ", " local authority "));
    }

    [Fact]
    public void Format_replaces_name_in_a_custom_heading()
    {
        var result = ComplexFieldConfirmationTitle.Format("Is this the correct {name}?", "local authority");

        Assert.Equal("Is this the correct local authority?", result);
    }

    [Fact]
    public void Format_replaces_name_ignoring_the_placeholder_case()
    {
        Assert.Equal("Is this the correct local authority?", ComplexFieldConfirmationTitle.Format("Is this the correct {Name}?", "local authority"));
    }
}
