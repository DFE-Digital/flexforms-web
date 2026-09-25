using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Lsrp;

public sealed class UploadYourLSRPDataTemplateTask(IPage page) : TaskPage(page)
{
    protected override string TaskItem => "group-financial-summary-task-upload-your-local-send-reform-plan-data-template";

    public async Task CompleteAsync()
    {
        await UploadFileAsync("LocalSENDReformPlanDataTemplate");
        await MarkCompleteAndSaveAsync();
    }
}
