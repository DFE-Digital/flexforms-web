using GovUK.Dfe.FlexForms.Application.Interfaces;

namespace GovUK.Dfe.FlexForms.Application.Tests;

internal sealed class InMemoryFormSessionStore : IFormSessionStore
{
    private readonly Dictionary<string, string> _data = new(StringComparer.Ordinal);

    public string? GetString(string key) => _data.TryGetValue(key, out var value) ? value : null;

    public void SetString(string key, string value) => _data[key] = value;

    public void Remove(string key) => _data.Remove(key);

    public IReadOnlyCollection<string> Keys => _data.Keys.ToList();
}
