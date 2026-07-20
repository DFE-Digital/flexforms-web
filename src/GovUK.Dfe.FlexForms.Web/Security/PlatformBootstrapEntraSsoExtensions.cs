using GovUK.Dfe.CoreLibs.Security.Configurations;
using GovUK.Dfe.CoreLibs.Security.EntraSso;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Registers the Entra SSO OIDC scheme for platform bootstrap even when host
/// <c>EntraSso.Enabled</c> is false, so tenants (e.g. RGVisits) can enable Entra per request.
/// </summary>
public static class PlatformBootstrapEntraSsoExtensions
{
    /// <summary>
    /// Adds the Entra SSO OpenID Connect scheme using bootstrap placeholders; runtime
    /// tenant settings are applied via OIDC events.
    /// </summary>
    public static AuthenticationBuilder AddPlatformBootstrapEntraSso(
        this AuthenticationBuilder builder,
        IConfiguration configuration,
        OpenIdConnectEvents? customEvents = null)
    {
        var section = configuration.GetSection(EntraSsoDefaults.ConfigurationSection);
        builder.Services.Configure<EntraSsoOptions>(section);

        var opts = section.Get<EntraSsoOptions>() ?? new EntraSsoOptions();
        var authority = string.IsNullOrWhiteSpace(opts.TenantId) ||
                        opts.TenantId == Guid.Empty.ToString()
            ? "https://login.microsoftonline.com/common/v2.0"
            : opts.Authority;

        return builder.AddOpenIdConnect(EntraSsoDefaults.AuthenticationScheme, "Microsoft Entra ID", oidc =>
        {
            oidc.Authority = authority;
            oidc.ClientId = string.IsNullOrWhiteSpace(opts.ClientId)
                ? "platform-bootstrap-placeholder"
                : opts.ClientId;
            oidc.ClientSecret = opts.ClientSecret;
            oidc.RequireHttpsMetadata = opts.RequireHttpsMetadata;
            oidc.ResponseType = opts.ResponseType;
            oidc.GetClaimsFromUserInfoEndpoint = opts.GetClaimsFromUserInfoEndpoint;
            oidc.SaveTokens = opts.SaveTokens;
            oidc.UseTokenLifetime = opts.UseTokenLifetime;
            oidc.CallbackPath = opts.CallbackPath;
            oidc.SignedOutCallbackPath = opts.SignedOutCallbackPath;

            oidc.TokenValidationParameters = new TokenValidationParameters
            {
                NameClaimType = opts.NameClaimType,
                ValidateIssuer = true
            };

            oidc.Scope.Clear();
            foreach (var scope in opts.Scopes)
            {
                oidc.Scope.Add(scope);
            }

            oidc.Events = customEvents ?? new OpenIdConnectEvents();
        });
    }
}
