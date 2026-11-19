using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Support;

public static class FileValidationMessage
{
    public const string Markdown =
        "**Wrong template.** Use the *latest version* and check:\n" +
        "\n" +
        "- the URN\n" +
        "- the signature\n" +
        "\n" +
        "1. Download the template\n" +
        "2. Upload it again";

    public static async Task ExpectFormattedAsync(ILocator message)
    {
        await Assertions.Expect(message.Locator("strong")).ToHaveTextAsync("Wrong template.");
        await Assertions.Expect(message.Locator("em")).ToHaveTextAsync("latest version");

        await Assertions.Expect(message.Locator("ul.govuk-list--bullet > li"))
            .ToHaveTextAsync(["the URN", "the signature"]);
        await Assertions.Expect(message.Locator("ol.govuk-list--number > li"))
            .ToHaveTextAsync(["Download the template", "Upload it again"]);
    }
}