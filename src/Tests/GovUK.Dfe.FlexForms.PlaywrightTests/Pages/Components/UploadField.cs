using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Components;

/// <summary>An upload field on a form page: the Choose a file form and the Uploaded files table.</summary>
public sealed class UploadField(IPage page, string fieldId) : BasePage(page)
{
    public static string UniqueFileName(string label) => $"{label}-{Guid.NewGuid():N}.pdf";

    /// <summary>Chooses a file and presses Upload file. It doesn't wait for the upload to be accepted.</summary>
    public async Task UploadAsync(string fileName, byte[]? content = null)
    {
        await ById($"choose-file-{fieldId}").SetInputFilesAsync(new FilePayload
        {
            Name = fileName,
            MimeType = "application/pdf",
            Buffer = content ?? await File.ReadAllBytesAsync(FormPage.UploadFixture),
        });
        await ById($"submit-upload-file-{fieldId}").ClickAsync();
    }

    public async Task ExpectFileListedAsync(string fileName) =>
        await Assertions.Expect(FileRow(fileName))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 });

    public async Task ExpectFileCountAsync(int count) =>
        await Assertions.Expect(FileRows()).ToHaveCountAsync(count);

    public async Task ExpectErrorAsync(string message)
    {
        await Assertions.Expect(Page.Locator(".govuk-error-summary")).ToContainTextAsync(message);
        await Assertions.Expect(Page.Locator(".govuk-error-message")).ToContainTextAsync(message);
    }

    /// <summary>Waits for the file's validation status, which can arrive after the page loads.</summary>
    public async Task ExpectValidationStatusAsync(string fileName, string status) =>
        await Assertions.Expect(ValidationStatus(fileName).Locator("span").First)
            .ToHaveTextAsync(status, new LocatorAssertionsToHaveTextOptions { Timeout = 20_000 });

    /// <summary>The Status cell for the file: the status, then any message from the file validator.</summary>
    public ILocator ValidationStatus(string fileName) => FileRow(fileName).Locator("[data-file-validation-status]");

    public async Task DeleteAsync(string fileName)
    {
        await ById($"delete-upload-{fileName}").ClickAsync();

        // Deleting asks for confirmation on its own page, then returns here.
        await ById("confirmed-yes").CheckAsync();
        await ById("confirmation-continue").ClickAsync();
        await Assertions.Expect(FileRow(fileName)).ToHaveCountAsync(0);
    }

    private ILocator FileRows() =>
        Page.Locator(".govuk-table__body .govuk-table__row")
            .Filter(new LocatorFilterOptions { Has = Page.Locator("[id^=\"download-\"]") });

    private ILocator FileRow(string fileName) =>
        FileRows().Filter(new LocatorFilterOptions { Has = ById($"download-{fileName}") });
}
