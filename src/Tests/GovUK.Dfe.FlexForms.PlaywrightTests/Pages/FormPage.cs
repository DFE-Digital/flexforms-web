using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages;

public abstract class FormPage(IPage page)
{
    public static readonly string UploadFixture = Path.Combine(AppContext.BaseDirectory, "assets", "upload.pdf");

    protected IPage Page { get; } = page;

    protected ILocator ByIdData(string id) => ById($"Data_{id}");

    protected ILocator ById(string id) => Page.Locator($"[id=\"{id}\"]");

    protected async Task SaveAndContinueAsync() => await SaveAndContinueButton().ClickAsync();

    protected async Task MarkCompleteAndSaveAsync()
    {
        await TaskCompletedCheckbox().CheckAsync();
        await SaveTaskSummaryButton().ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new Regex(@"/applications/[^/]+$"));
    }

    protected async Task ConfirmContinueAsync() => await ConfirmationContinueButton().ClickAsync();

    protected async Task ConfirmYesAndContinueAsync()
    {
        await ConfirmedYesRadio().CheckAsync();
        await ConfirmContinueAsync();
    }

    protected async Task SearchAutocompleteAsync(string inputId, string searchText, string? optionText = null)
    {
        optionText ??= searchText;
        var input = AutocompleteInput(inputId);

        // The field defers the last step of its setup behind a 100ms timeout, and that step
        // ends by hiding the menu unconditionally. Searching before it runs means the results
        // arrive first and then get hidden, leaving options present but unclickable. The
        // govuk-input class is added in that same deferred step, so it marks the field ready.
        await Assertions.Expect(input).ToHaveClassAsync(new Regex("govuk-input"));
        await input.ClickAsync();

        // The field fires one request per input event with no debounce and no stale-response
        // guard, so typing character by character lets an earlier reply repopulate the menu
        // last. Filling in one go issues a single request for the complete query.
        var resultsTask = Page.WaitForResponseAsync(response =>
            response.Url.Contains("handler=complexField") &&
            response.Url.Contains($"query={Uri.EscapeDataString(searchText)}"));
        await input.FillAsync(searchText);
        await resultsTask;

        await AutocompleteOption(inputId, optionText).ClickAsync();
        await AutocompleteConfirmButton().ClickAsync();
    }

    protected async Task UploadFileAsync(string fieldId, string? filePath = null)
    {
        filePath ??= UploadFixture;

        // each upload needs a unique name
        var extension = Path.GetExtension(filePath);
        var baseName = Path.GetFileNameWithoutExtension(filePath);
        var fileName = $"{baseName}-{fieldId}{extension}";

        await UploadFileInput(fieldId).SetInputFilesAsync(new FilePayload
        {
            Name = fileName,
            MimeType = extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf" : "application/octet-stream",
            Buffer = await File.ReadAllBytesAsync(filePath),
        });
        await SubmitUploadButton(fieldId).ClickAsync();
        await Assertions.Expect(DownloadLink(fileName))
            .ToContainTextAsync(fileName, new LocatorAssertionsToContainTextOptions { Timeout = 15_000 });
        await SubmitFieldButton(fieldId).ClickAsync();
    }

    protected async Task EnterDateAsync(string prefix, string day, string month, string year)
    {
        await DateDayInput(prefix).FillAsync(day);
        await DateMonthInput(prefix).FillAsync(month);
        await DateYearInput(prefix).FillAsync(year);
    }

    private ILocator SaveAndContinueButton() => ById("save-and-continue-button");

    private ILocator TaskCompletedCheckbox() => Page.GetByLabel("Mark this section as complete, it's ready for review");

    private ILocator SaveTaskSummaryButton() => ById("save-task-summary-button");

    private ILocator ConfirmationContinueButton() => Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Continue" });

    private ILocator ConfirmedYesRadio() => Page.GetByRole(AriaRole.Radio, new PageGetByRoleOptions { Name = "Yes" });

    private ILocator AutocompleteInput(string inputId) => ById(inputId);

    private ILocator AutocompleteOption(string inputId, string optionText) =>
        ById($"{inputId}-container").Locator(".autocomplete__option").Filter(new LocatorFilterOptions { HasText = optionText }).First;

    private ILocator AutocompleteConfirmButton() => ById("autocomplete-confirm-button");

    private ILocator UploadFileInput(string fieldId) => ById($"upload-file-{fieldId}");

    private ILocator SubmitUploadButton(string fieldId) => ById($"submit-upload-file-{fieldId}");

    private ILocator DownloadLink(string fileName) => ById($"download-{fileName}");

    private ILocator SubmitFieldButton(string fieldId) => ById($"submit-{fieldId}");

    private ILocator DateDayInput(string prefix) => ById($"{prefix}.Day");

    private ILocator DateMonthInput(string prefix) => ById($"{prefix}.Month");

    private ILocator DateYearInput(string prefix) => ById($"{prefix}.Year");
}
