using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Forces all cookie auth schemes to use the distributed ticket store so cookies stay tiny.
/// </summary>
public sealed class ConfigureCookieTicketStore : IPostConfigureOptions<CookieAuthenticationOptions>
{
    private readonly ITicketStore _store;

    public ConfigureCookieTicketStore(ITicketStore store)
    {
        _store = store;
    }

    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        options.SessionStore = _store;
    }
}


