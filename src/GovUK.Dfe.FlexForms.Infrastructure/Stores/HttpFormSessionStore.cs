using GovUK.Dfe.FlexForms.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace GovUK.Dfe.FlexForms.Infrastructure.Stores;

/// <summary>
/// HTTP-session adapter for <see cref="IFormSessionStore"/>.
/// </summary>
public sealed class HttpFormSessionStore(IHttpContextAccessor httpContextAccessor) : IFormSessionStore
{
    private ISession Session =>
        httpContextAccessor.HttpContext?.Session
        ?? throw new InvalidOperationException("HTTP session is not available.");

    public string? GetString(string key) => Session.GetString(key);

    public void SetString(string key, string value) => Session.SetString(key, value);

    public void Remove(string key) => Session.Remove(key);

    public IReadOnlyCollection<string> Keys => Session.Keys.ToList();
}
