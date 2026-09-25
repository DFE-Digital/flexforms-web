using System.Text.Json.Serialization;

namespace GovUK.Dfe.FlexForms.Domain.Models;

/// <summary>
/// Controls where the user goes after Save and Continue on a page.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NavigationAfterSave
{
    /// <summary>
    /// Always return to the task summary.
    /// </summary>
    [JsonStringEnumMemberName("summary")]
    Summary,

    /// <summary>
    /// Continue to the next visible page in the task (linear wizard). Falls back to summary at the end.
    /// </summary>
    [JsonStringEnumMemberName("linear")]
    Linear,

    /// <summary>
    /// Continue only to a page revealed by conditional logic from this page's answers; otherwise summary.
    /// </summary>
    [JsonStringEnumMemberName("branch")]
    Branch
}
