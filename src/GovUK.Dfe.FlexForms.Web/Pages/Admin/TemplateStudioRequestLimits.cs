namespace GovUK.Dfe.FlexForms.Web.Pages.Admin;

/// <summary>
/// Template Studio posts a whole template as JSON. 8 MiB is far above real templates (the largest is a few hundred KB)
/// and is the most a request may send.
/// </summary>
internal static class TemplateStudioRequestLimits
{
    public const int MaxBytes = 8_388_608;
}
