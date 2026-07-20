namespace GovUK.Dfe.FlexForms.Application.Exceptions;

/// <summary>
/// Thrown when an application cannot be loaded because it does not exist or the current user
/// does not have permission to view it. Handlers map this to a 404 response.
/// </summary>
public sealed class ApplicationAccessException(string applicationReference) : Exception(
    $"Application '{applicationReference}' was not found or is not accessible.")
{
    /// <summary>
    /// The application reference that could not be accessed.
    /// </summary>
    public string ApplicationReference { get; } = applicationReference;
}
