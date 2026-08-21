using GovUK.Dfe.FlexForms.Web.Pages.Admin;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages.Admin;

public class ContributorManagementEmailEncodingTests
{
    [Fact]
    public void BuildEmailLookupPath_EncodesPlusAsPercent2B()
    {
        var path = ContributorManagementModel.BuildEmailLookupPath("farshad+23423@test.com", currentPage: 1);

        Assert.Equal(
            "/admin/contributor-management?email=farshad%2B23423%40test.com&currentPage=1",
            path);

        var encodedValue = path.Split("email=")[1].Split('&')[0];
        Assert.Equal("farshad+23423@test.com", Uri.UnescapeDataString(encodedValue));
    }

    [Fact]
    public void BuildEmailLookupPath_EncodesOtherReservedCharacters()
    {
        var path = ContributorManagementModel.BuildEmailLookupPath("a&b=c@example.com", currentPage: 3);

        Assert.Equal(
            "/admin/contributor-management?email=a%26b%3Dc%40example.com&currentPage=3",
            path);
    }
}
