using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using AspNetOpenIdConnectOptions = Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Applies DfE Sign-In settings from the current tenant configuration to OIDC options at runtime.
/// </summary>
public static class TenantAwareOpenIdConnectConfigurator
{
    /// <summary>
    /// Overlays tenant-specific DfE Sign-In settings onto the active OIDC options instance.
    /// Must run on both challenge (<c>OnRedirectToIdentityProvider</c>) and callback
    /// (<c>OnMessageReceived</c>) so ClientId / audience match the id_token.
    /// </summary>
    public static void ApplyTenantSettings(HttpContext httpContext, AspNetOpenIdConnectOptions options)
    {
        var section = GetTenantSignInSection(httpContext);
        if (section is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(section["Authority"]))
        {
            var authority = section["Authority"]!.TrimEnd('/');
            var metadataAddress = $"{authority}/.well-known/openid-configuration";
            if (NeedsOidcDiscoveryRefresh(options, authority, metadataAddress))
            {
                options.Authority = authority;
                options.MetadataAddress = metadataAddress;
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
            // id_token aud is the client id; startup options still hold the bootstrap/Transfers audience.
            options.TokenValidationParameters.ValidAudience = section["ClientId"];
            options.TokenValidationParameters.ValidAudiences = [section["ClientId"]!];
            options.TokenValidationParameters.ValidateAudience = true;
        }

        if (!string.IsNullOrWhiteSpace(section["ClientSecret"]))
        {
            options.ClientSecret = section["ClientSecret"];
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

        if (!string.IsNullOrWhiteSpace(section["RedirectUri"]))
        {
            options.CallbackPath = ExtractCallbackPath(section["RedirectUri"]);
        }
    }

    /// <summary>
    /// Applies tenant DfE Sign-In settings to the outbound OIDC protocol message.
    /// Required because ASP.NET Core copies <c>Options.ClientId</c> into
    /// <see cref="RedirectContext.ProtocolMessage"/> before this event runs — after switching
    /// hosts, Options may still hold another tenant's ClientId (e.g. Transfers
    /// <c>RSDExternalApps</c> with LSRP <c>redirect_uri</c>).
    /// </summary>
    public static async Task ApplyProtocolMessageAsync(RedirectContext context)
    {
        ApplyTenantSettings(context.HttpContext, context.Options);

        var section = GetTenantSignInSection(context.HttpContext);
        if (section is null)
        {
            throw new InvalidOperationException(
                "Tenant DfESignIn configuration is missing. " +
                "Map this hostname in TenantConfig and ensure the DfESignIn category is present. " +
                "Locally prefer the Lsrp-https or Visits-https launch profile (lsrp.localhost / rgvisits.localhost).");
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
        // back to /signin-oidc and cause "Correlation failed" on the remote login handler.
        if (!isLogout && !string.IsNullOrWhiteSpace(section["RedirectUri"]))
        {
            context.ProtocolMessage.RedirectUri = section["RedirectUri"];
        }

        if (!isLogout && !string.IsNullOrWhiteSpace(section["Prompt"]))
        {
            context.ProtocolMessage.Prompt = section["Prompt"];
        }

        if (!isLogout)
        {
            var scopes = section.GetSection("Scopes").Get<string[]>();
            if (scopes?.Length > 0)
            {
                context.ProtocolMessage.Scope = string.Join(' ', scopes);
            }
        }

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
            }
            else if (!string.IsNullOrWhiteSpace(configuration.AuthorizationEndpoint))
            {
                context.ProtocolMessage.IssuerAddress = configuration.AuthorizationEndpoint;
            }
        }

        if (!isLogout && string.IsNullOrWhiteSpace(context.ProtocolMessage.IssuerAddress))
        {
            throw new InvalidOperationException(
                "Cannot redirect to the DfE Sign-In authorization endpoint. " +
                "Check the tenant's DfESignIn:Authority setting (and that discovery is reachable). " +
                "Locally use lsrp.localhost / rgvisits.localhost launch profiles rather than plain localhost unless localhost is mapped in TenantConfig.");
        }
    }

    /// <summary>
    /// Returns the tenant DfE Sign-In configuration section when platform bootstrap is active.
    /// </summary>
    public static IConfigurationSection? GetTenantSignInSection(HttpContext httpContext)
    {
        var tenantContext = httpContext.RequestServices.GetService<Tenancy.ITenantRequestContext>();
        var section = tenantContext?.TenantConfiguration?.GetSection("DfESignIn");
        return section?.Exists() == true ? section : null;
    }

    /// <summary>
    /// True when OIDC discovery must be (re)bound — authority/metadata changed, or the
    /// bootstrap <see cref="StaticConfigurationManager{T}"/> stub is still installed
    /// (it has no AuthorizationEndpoint and causes challenge failures).
    /// </summary>
    internal static bool NeedsOidcDiscoveryRefresh(
        AspNetOpenIdConnectOptions options,
        string authority,
        string metadataAddress)
    {
        if (!string.Equals(options.Authority, authority, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.Equals(options.MetadataAddress, metadataAddress, StringComparison.OrdinalIgnoreCase))
            return true;

        if (options.ConfigurationManager is null)
            return true;

        if (options.ConfigurationManager is StaticConfigurationManager<OpenIdConnectConfiguration>)
            return true;

        if (options.Configuration is not null
            && string.IsNullOrWhiteSpace(options.Configuration.AuthorizationEndpoint))
            return true;

        return false;
    }

    private static PathString ExtractCallbackPath(string redirectUri)
    {
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri))
        {
            return new PathString("/signin-oidc");
        }

        return new PathString(uri.AbsolutePath);
    }
}
