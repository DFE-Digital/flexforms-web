using QRCoder;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Presentation helpers for Test Authentication authenticator app setup: the QR code (a PNG data URI, allowed by the
/// CSP <c>img-src data:</c>) and the key in groups of four.
/// </summary>
public static class AuthenticatorQrCode
{
    private const string AuthenticatorUriPrefix = "otpauth://totp/";

    /// <summary>
    /// Real URIs (issuer, email of up to 254 characters, 32 character key) stay well under this. It is far below
    /// the 1,663 bytes a QR code holds at error correction level Q, so <see cref="ToPngDataUri"/> cannot overflow.
    /// </summary>
    public const int MaxAuthenticatorUriLength = 1024;

    /// <summary>
    /// <c>true</c> for an <c>otpauth://totp/</c> URI short enough to draw. Pages post the URI back to redraw the QR
    /// code, so anything else is rejected rather than drawn.
    /// </summary>
    public static bool IsAuthenticatorUri(string? value)
        => value is not null
           && value.Length <= MaxAuthenticatorUriLength
           && value.StartsWith(AuthenticatorUriPrefix, StringComparison.Ordinal);

    public static string ToPngDataUri(string authenticatorUri)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(authenticatorUri, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(5);
        return "data:image/png;base64," + Convert.ToBase64String(png);
    }

    public static string FormatKey(string? sharedKey)
        => string.Join(' ', (sharedKey ?? string.Empty).Chunk(4).Select(chunk => new string(chunk)));
}
