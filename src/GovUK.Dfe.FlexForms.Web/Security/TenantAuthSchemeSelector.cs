using GovUK.Dfe.FlexForms.Web.Tenancy;
using GovUK.Dfe.CoreLibs.Security.Configurations;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Interactive login schemes that can be selected per tenant at request time
/// (no platform restart required after Tenant Settings changes).
/// </summary>
public enum InteractiveAuthScheme
{
    DfESignIn = 0,
    EntraSso = 1,
    TestAuthentication = 2
}

/// <summary>
/// Resolves the interactive IdP for the current request from tenant configuration
/// (platform bootstrap) with host options as fallback.
/// </summary>
public static class TenantAuthSchemeSelector
{
    public const string AuthenticationSectionName = "Authentication";
    public const string SchemeKey = "Authentication:Scheme";
    public const string InteractiveSchemeKey = "InteractiveAuthentication:Scheme";

    /// <summary>
    /// Resolves which interactive auth scheme to use for the current request.
    /// <list type="number">
    /// <item><description>
    /// Explicit <c>Authentication:Scheme</c> (or <c>InteractiveAuthentication:Scheme</c>)
    /// when set — use this when Test, DfE Sign-In, and Entra are all configured/enabled.
    /// Values: <c>Test</c> / <c>TestAuthentication</c>, <c>Entra</c> / <c>EntraSso</c>,
    /// <c>DfESignIn</c> / <c>DSI</c> / <c>OpenIdConnect</c>.
    /// </description></item>
    /// <item><description>Else if <c>TestAuthentication:Enabled</c> → Test</description></item>
    /// <item><description>Else if <c>EntraSso:Enabled</c> → Entra</description></item>
    /// <item><description>Else DfE Sign-In</description></item>
    /// </list>
    /// </summary>
    public static InteractiveAuthScheme Resolve(
        HttpContext? httpContext,
        IOptions<TestAuthenticationOptions>? hostTestOptions = null,
        IOptions<EntraSsoOptions>? hostEntraOptions = null)
    {
        if (TryParseScheme(ReadExplicitScheme(httpContext), out var explicitScheme))
        {
            return explicitScheme;
        }

        if (IsTestAuthenticationEnabled(httpContext, hostTestOptions))
        {
            return InteractiveAuthScheme.TestAuthentication;
        }

        if (IsEntraSsoEnabled(httpContext, hostEntraOptions))
        {
            return InteractiveAuthScheme.EntraSso;
        }

        return InteractiveAuthScheme.DfESignIn;
    }

    /// <summary>
    /// Returns <c>true</c> when Entra SSO should be used for the current request.
    /// Prefer <see cref="Resolve"/> when choosing between Test / Entra / DfE Sign-In.
    /// </summary>
    public static bool IsEntraSsoEnabled(HttpContext? httpContext, IOptions<EntraSsoOptions>? hostEntraOptions = null)
    {
        if (TryGetTenantBool(httpContext, "EntraSso:Enabled", out var tenantEnabled))
        {
            return tenantEnabled;
        }

        return hostEntraOptions?.Value.Enabled ?? false;
    }

    /// <summary>
    /// Returns <c>true</c> when Test Authentication is enabled for the current tenant (or host fallback).
    /// </summary>
    public static bool IsTestAuthenticationEnabled(
        HttpContext? httpContext,
        IOptions<TestAuthenticationOptions>? hostTestOptions = null)
    {
        if (TryGetTenantBool(httpContext, "TestAuthentication:Enabled", out var tenantEnabled))
        {
            return tenantEnabled;
        }

        return hostTestOptions?.Value.Enabled ?? false;
    }

    public static bool IsTestAuthenticationActive(
        HttpContext? httpContext,
        IOptions<TestAuthenticationOptions>? hostTestOptions = null,
        IOptions<EntraSsoOptions>? hostEntraOptions = null)
        => Resolve(httpContext, hostTestOptions, hostEntraOptions) == InteractiveAuthScheme.TestAuthentication;

    private static string? ReadExplicitScheme(HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            return null;
        }

        var tenantContext = httpContext.RequestServices.GetService<ITenantRequestContext>();
        var config = tenantContext?.TenantConfiguration;
        if (config is null)
        {
            return null;
        }

        return FirstNonEmpty(
            config[SchemeKey],
            config[InteractiveSchemeKey],
            config.GetSection(AuthenticationSectionName)["Scheme"],
            config.GetSection("InteractiveAuthentication")["Scheme"]);
    }

    private static bool TryGetTenantBool(HttpContext? httpContext, string key, out bool value)
    {
        value = false;
        if (httpContext is null)
        {
            return false;
        }

        var tenantContext = httpContext.RequestServices.GetService<ITenantRequestContext>();
        var raw = tenantContext?.TenantConfiguration?[key];
        return bool.TryParse(raw, out value);
    }

    private static bool TryParseScheme(string? raw, out InteractiveAuthScheme scheme)
    {
        scheme = InteractiveAuthScheme.DfESignIn;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        switch (raw.Trim().ToLowerInvariant())
        {
            case "test":
            case "testauth":
            case "testauthentication":
                scheme = InteractiveAuthScheme.TestAuthentication;
                return true;

            case "entra":
            case "entrasso":
            case "entra-sso":
            case "microsoft":
                scheme = InteractiveAuthScheme.EntraSso;
                return true;

            case "dsi":
            case "dfesignin":
            case "dfe-signin":
            case "dfesign-in":
            case "openidconnect":
            case "oidc":
                scheme = InteractiveAuthScheme.DfESignIn;
                return true;

            default:
                return false;
        }
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
