using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Transfers;

public sealed class HighQualityInclusiveEducationPage(IPage page) : TaskPage(page)
{
    protected override string TaskItem =>
        "group-about-the-trust-that-academies-are-joining-task-high-quality-and-inclusive-education";

    public async Task CompleteAsync()
    {
        await ById("field-highqualityandinclusiveeducationquality-change-link").ClickAsync();
        await ById("Data_highQualityAndInclusiveEducationQuality").FillAsync(
            "High quality and inclusive education quality testing text");
        await SaveAndContinueAsync();

        await ById("field-highqualityandinclusiveeducationimpact-change-link").ClickAsync();
        await ById("Data_highQualityAndInclusiveEducationImpact").FillAsync(
            "High quality and inclusive education impact testing text");
        await SaveAndContinueAsync();

        await MarkCompleteAndSaveAsync();
    }
}
