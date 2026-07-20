using GovUK.Dfe.FlexForms.Web.Tenancy;
using GovUK.Dfe.CoreLibs.Security.Configurations;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using AspNetOpenIdConnectOptions = Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Applies tenant <c>EntraSso</c> settings onto the Entra OIDC options at challenge and callback time.
/// </summary>
public static class TenantAwareEntraSsoConfigurator
{
    /// <summary>
    /// Overlays the current tenant's Entra SSO settings onto <paramref name="options"/>.
    /// Must run on both <c>OnRedirectToIdentityProvider</c> and <c>OnMessageReceived</c>.
    /// </summary>
    public static void ApplyTenantSettings(HttpContext httpContext, AspNetOpenIdConnectOptions options)
    {
        var section = GetTenantEntraSection(httpContext);
        if (section?.Exists() != true)
        {
            return;
        }

        var instance = section["Instance"];
        if (string.IsNullOrWhiteSpace(instance))
        {
            instance = "https://login.microsoftonline.com/";
        }

        var tenantId = section["TenantId"];
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            var authority = $"{instance.TrimEnd('/')}/{tenantId}/v2.0";
            if (!string.Equals(options.Authority, authority, StringComparison.OrdinalIgnoreCase))
            {
                options.Authority = authority;
                options.MetadataAddress = $"{authority}/.well-known/openid-configuration";
                // Reset discovery cache so a previous host/placeholder tenant is not reused.
                options.Configuration = null;
                options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                    options.MetadataAddress,
                    new OpenIdConnectConfigurationRetriever(),
                    new HttpDocumentRetriever { RequireHttps = options.RequireHttpsMetadata });
            }
        }

        if (!string.IsNullOrWhiteSpace(section["ClientId"]))
        {
            options.ClientId = section["ClientId"];
            options.TokenValidationParameters.ValidAudience = section["ClientId"];
            options.TokenValidationParameters.ValidAudiences = [section["ClientId"]!];
            options.TokenValidationParameters.ValidateAudience = true;
            options.TokenValidationParameters.NameClaimType =
                section["NameClaimType"] ?? "preferred_username";
        }

        if (!string.IsNullOrWhiteSpace(section["ClientSecret"]))
        {
            options.ClientSecret = section["ClientSecret"];
        }

        if (!string.IsNullOrWhiteSpace(section["CallbackPath"]))
        {
            options.CallbackPath = section["CallbackPath"];
        }
        else if (!string.IsNullOrWhiteSpace(section["RedirectUri"]) &&
                 Uri.TryCreate(section["RedirectUri"], UriKind.Absolute, out var redirectUri))
        {
            options.CallbackPath = redirectUri.AbsolutePath;
        }

        if (!string.IsNullOrWhiteSpace(section["SignedOutCallbackPath"]))
        {
            options.SignedOutCallbackPath = section["SignedOutCallbackPath"];
        }

        if (!string.IsNullOrWhiteSpace(section["ResponseType"]))
        {
            options.ResponseType = section["ResponseType"]!;
        }

        var scopes = section.GetSection("Scopes").Get<string[]>();
        if (scopes?.Length > 0)
        {
            options.Scope.Clear();
            foreach (var scope in scopes)
            {
                options.Scope.Add(scope);
            }
        }

        if (bool.TryParse(section["SaveTokens"], out var saveTokens))
        {
            options.SaveTokens = saveTokens;
        }

        if (bool.TryParse(section["GetClaimsFromUserInfoEndpoint"], out var getClaims))
        {
            options.GetClaimsFromUserInfoEndpoint = getClaims;
        }

        if (bool.TryParse(section["RequireHttpsMetadata"], out var requireHttps))
        {
            options.RequireHttpsMetadata = requireHttps;
        }

        if (bool.TryParse(section["UseTokenLifetime"], out var useTokenLifetime))
        {
            options.UseTokenLifetime = useTokenLifetime;
        }
    }

    /// <summary>
    /// Returns the tenant Entra SSO configuration section when present.
    /// </summary>
    public static IConfigurationSection? GetTenantEntraSection(HttpContext httpContext)
    {
        var tenantContext = httpContext.RequestServices.GetService<ITenantRequestContext>();
        var section = tenantContext?.TenantConfiguration?.GetSection(EntraSsoOptions.SectionName);
        return section?.Exists() == true ? section : null;
    }

    /// <summary>
    /// Applies tenant Entra settings to the outbound OIDC protocol message.
    /// Required because ASP.NET Core builds <see cref="RedirectContext.ProtocolMessage"/>
    /// from host options before <c>OnRedirectToIdentityProvider</c> runs — updating
    /// <see cref="AspNetOpenIdConnectOptions.ClientId"/> alone leaves the bootstrap placeholder
    /// in the authorize URL.
    /// </summary>
    public static async Task ApplyProtocolMessageAsync(RedirectContext context)
    {
        ApplyTenantSettings(context.HttpContext, context.Options);

        var section = GetTenantEntraSection(context.HttpContext);
        if (section?.Exists() != true)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(section["ClientId"]))
        {
            context.ProtocolMessage.ClientId = section["ClientId"];
        }

        // ASP.NET Core does not set RequestType=Logout before ForSignOut events; detect via
        // post_logout_redirect_uri so we never overwrite end_session with authorize.
        var isLogout = context.ProtocolMessage.RequestType == OpenIdConnectRequestType.Logout
            || !string.IsNullOrEmpty(context.ProtocolMessage.PostLogoutRedirectUri);

        // RedirectUri is for authorize/login only. Setting it during logout can send the IdP
        // back to the sign-in callback and cause silent re-login.
        if (!isLogout && !string.IsNullOrWhiteSpace(section["RedirectUri"]))
        {
            context.ProtocolMessage.RedirectUri = section["RedirectUri"];
        }

        if (!isLogout)
        {
            var scopes = section.GetSection("Scopes").Get<string[]>();
            if (scopes?.Length > 0)
            {
                context.ProtocolMessage.Scope = string.Join(' ', scopes);
            }
        }

        // Prefer AuthorizationEndpoint / EndSessionEndpoint for the tenant Authority (not bootstrap /common).
        if (context.Options.ConfigurationManager is not null)
        {
            var configuration = await context.Options.ConfigurationManager
                .GetConfigurationAsync(context.HttpContext.RequestAborted);
            context.Options.Configuration = configuration;

            if (isLogout)
            {
                if (!string.IsNullOrWhiteSpace(configuration.EndSessionEndpoint))
                {
                    context.ProtocolMessage.IssuerAddress = configuration.EndSessionEndpoint;
                }
                else
                {
                    context.ProtocolMessage.IssuerAddress = BuildEntraLogoutEndpoint(context.Options.Authority)
                        ?? context.ProtocolMessage.IssuerAddress;
                }
            }
            else if (!string.IsNullOrWhiteSpace(configuration.AuthorizationEndpoint))
            {
                context.ProtocolMessage.IssuerAddress = configuration.AuthorizationEndpoint;
            }
        }
    }

    /// <summary>
    /// Builds the Entra ID logout endpoint from an authority such as
    /// <c>https://login.microsoftonline.com/{tenant}/v2.0</c>.
    /// </summary>
    private static string? BuildEntraLogoutEndpoint(string? authority)
    {
        if (string.IsNullOrWhiteSpace(authority))
        {
            return null;
        }

        var trimmed = authority.TrimEnd('/');
        const string v2Suffix = "/v2.0";
        if (trimmed.EndsWith(v2Suffix, StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^v2Suffix.Length];
        }

        return $"{trimmed}/oauth2/v2.0/logout";
    }

}
