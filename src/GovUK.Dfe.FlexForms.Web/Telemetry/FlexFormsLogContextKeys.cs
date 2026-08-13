namespace GovUK.Dfe.FlexForms.Web.Telemetry;

/// <summary>
/// FlexForms-specific structured log / App Insights property names.
/// Keep these out of GovUK.Dfe.CoreLibs.Http so the NuGet stays product-agnostic.
/// </summary>
public static class FlexFormsLogContextKeys
{
    public const string TemplateId = "TemplateId";
    public const string ApplicationId = "ApplicationId";
    public const string ApplicationReference = "ApplicationReference";
}
