namespace GovUK.Dfe.FlexForms.Application.Interfaces;

/// <summary>
/// Application port for request-scoped form session state.
/// Implemented in Infrastructure against HTTP session.
/// </summary>
public interface IFormSessionStore
{
    string? GetString(string key);

    void SetString(string key, string value);

    void Remove(string key);

    IReadOnlyCollection<string> Keys { get; }
}
