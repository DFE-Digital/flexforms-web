namespace GovUK.Dfe.FlexForms.Application.Exceptions;

/// <summary>
/// Thrown when a response save is attempted but the session form data does not belong to the
/// application being saved (another application was opened in a different tab, or the session
/// was lost). Saving in that state would overwrite the application with partial or foreign data.
/// </summary>
public sealed class StaleFormSessionException(Guid applicationId, string? sessionApplicationId) : Exception(
    $"Session form data belongs to '{sessionApplicationId ?? "<none>"}', not application '{applicationId}'. Save refused.")
{
    public Guid ApplicationId { get; } = applicationId;

    public string? SessionApplicationId { get; } = sessionApplicationId;
}
