namespace GovUK.Dfe.FlexForms.Web.Features;

/// <summary>
/// Feature names read from the <c>FeatureManagement</c> tenant setting (target Web or Shared), for example
/// <c>{"TemplateStudio": true}</c>. A feature the tenant doesn't set falls back to appsettings, where it is off.
/// </summary>
public static class FeatureFlags
{
    /// <summary>The Template Studio editor, its preview, and the buttons that open it.</summary>
    public const string TemplateStudio = "TemplateStudio";
}
