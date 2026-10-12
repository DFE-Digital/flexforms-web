using Microsoft.Extensions.Configuration;

namespace GovUK.Dfe.FlexForms.Web.Extensions;

/// <summary>
/// Response security headers (CSP, Referrer-Policy, Permissions-Policy, framing, MIME sniffing).
/// </summary>
public static class SecurityHeadersExtensions
{
    public const string EnforceCspConfigKey = "SecurityHeaders:EnforceContentSecurityPolicy";

    /// <summary>
    /// CSP is enforced by default. Setting <see cref="EnforceCspConfigKey"/> to false downgrades it to
    /// Report-Only, as an escape hatch if a page is found to break in an environment.
    /// Inline scripts need <c>nonce="@ViewContext.HttpContext.GetNonce()"</c>; inline event handlers
    /// and <c>javascript:</c> URLs are blocked, so use the data-* hooks in <c>wwwroot/js/csp-handlers.js</c>.
    /// </summary>
    public static IApplicationBuilder UseFlexFormsSecurityHeaders(
        this IApplicationBuilder app,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var enforceCsp = configuration.GetValue(EnforceCspConfigKey, defaultValue: true);
        var allowLocalDevTooling = environment.IsDevelopment() || environment.IsEnvironment("Local");
        void ConfigureCsp(CspBuilder builder) => ConfigureContentSecurityPolicy(builder, allowLocalDevTooling);

        return app.UseSecurityHeaders(policies =>
        {
            policies
                .AddFrameOptionsDeny()
                .AddContentTypeOptionsNoSniff()
                .AddReferrerPolicyStrictOriginWhenCrossOrigin()
                .RemoveServerHeader()
                .AddPermissionsPolicy(builder =>
                {
                    builder.AddAccelerometer().None();
                    builder.AddAutoplay().None();
                    builder.AddCamera().None();
                    builder.AddEncryptedMedia().None();
                    builder.AddGeolocation().None();
                    builder.AddGyroscope().None();
                    builder.AddMagnetometer().None();
                    builder.AddMicrophone().None();
                    builder.AddMidi().None();
                    builder.AddPayment().None();
                    builder.AddPictureInPicture().None();
                    builder.AddUsb().None();
                });

            if (enforceCsp)
            {
                policies.AddContentSecurityPolicy(ConfigureCsp);
            }
            else
            {
                policies.AddContentSecurityPolicyReportOnly(ConfigureCsp);
            }
        });
    }

    private static void ConfigureContentSecurityPolicy(CspBuilder builder, bool allowLocalDevTooling)
    {
        builder.AddDefaultSrc().Self();
        var scriptSrc = builder.AddScriptSrc().Self().WithNonce().From("https://js.monitor.azure.com");
        // Inline style attributes are used widely; a nonce here would cause browsers to ignore 'unsafe-inline'.
        builder.AddStyleSrc().Self().UnsafeInline();
        builder.AddImgSrc().Self().Data();
        builder.AddFontSrc().Self().Data();
        // API base URL and SignalR host are tenant-configured, and App Insights ingestion is regional.
        var connectSrc = builder.AddConnectSrc().Self().From("https:").From("wss:");
        if (allowLocalDevTooling)
        {
            // Visual Studio hot reload / Browser Link talk to localhost over plain ws:// and http://.
            scriptSrc.From("http://localhost:*");
            connectSrc.From("ws://localhost:*").From("http://localhost:*");
        }
        builder.AddFormAction().Self().From("https:");
        builder.AddObjectSrc().None();
        builder.AddBaseUri().Self();
        builder.AddFrameAncestors().None();
    }
}
