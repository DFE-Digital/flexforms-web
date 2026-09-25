using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Transfers;

public sealed class SchoolImprovementPage(IPage page) : TaskPage(page)
{
    private const string UploadField = "schoolImprovementModel";

    protected override string TaskItem => "group-about-the-trust-that-academies-are-joining-task-school-improvement";

    public async Task CompleteAsync()
    {
        await ById("field-schoolimprovementmodel-change-link").ClickAsync();
        await UploadFileAsync(UploadField);
        await MarkCompleteAndSaveAsync();
    }
}
