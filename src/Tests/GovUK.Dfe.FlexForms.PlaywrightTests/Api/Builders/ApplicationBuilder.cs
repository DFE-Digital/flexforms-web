namespace GovUK.Dfe.FlexForms.PlaywrightTests.Api.Builders;

public static class ApplicationBuilder
{
    /// <summary>API expects an encoded (base64) JSON response body on create.</summary>
    private static readonly string EmptyEncodedResponseBody =
        ApiBase.EncodeJson("{}");

    public static CreateApplicationRequest CreateApplicationRequest(
        string templateId,
        string? initialResponseBody = null) =>
        new(templateId, initialResponseBody ?? EmptyEncodedResponseBody);
}
