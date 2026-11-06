namespace GovUK.Dfe.FlexForms.PlaywrightTests.Api.Builders;

public static class ApplicationBuilder
{
    /// <summary>
    /// Create stores InitialResponseBody verbatim (unlike add-response, which Base64-decodes),
    /// so send raw JSON exactly as the Web dashboard does.
    /// </summary>
    private const string EmptyResponseBody = "{}";

    public static CreateApplicationRequest CreateApplicationRequest(
        string templateId,
        string? initialResponseBody = null) =>
        new(templateId, initialResponseBody ?? EmptyResponseBody);
}
