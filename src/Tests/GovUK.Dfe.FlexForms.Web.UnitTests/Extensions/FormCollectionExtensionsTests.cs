using GovUK.Dfe.FlexForms.Web.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Extensions;

public class FormCollectionExtensionsTests
{
    [Fact]
    public void ToPostedFields_ShouldCopyKeysAndValues()
    {
        IFormCollection form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["name"] = "Ada",
            ["roles"] = new StringValues(["lead", "member"])
        });

        var fields = form.ToPostedFields();

        Assert.Equal(["Ada"], fields["name"]);
        Assert.Equal(["lead", "member"], fields["roles"]);
    }
}
