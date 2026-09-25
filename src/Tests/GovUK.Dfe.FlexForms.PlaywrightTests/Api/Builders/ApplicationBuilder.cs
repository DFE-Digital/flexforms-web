namespace GovUK.Dfe.FlexForms.PlaywrightTests.Api.Builders;

public static class ApplicationBuilder
{
    public static CreateApplicationRequest CreateApplicationRequest(string templateId, string? initialResponseBody = "{}") =>
        new(templateId, initialResponseBody);
}
