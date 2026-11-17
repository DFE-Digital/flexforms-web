namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Mutable view-state bag for the authenticator setup code admin page.
/// </summary>
public sealed class TestAuthAuthenticatorCodeWorkState
{
    public string Email { get; set; } = string.Empty;

    /// <summary>The created code. Shown once and never stored by Web.</summary>
    public string? Code { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }
}
