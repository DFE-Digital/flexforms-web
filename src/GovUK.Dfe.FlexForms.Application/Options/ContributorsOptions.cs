namespace GovUK.Dfe.FlexForms.Application.Options;

/// <summary>
/// Tenant-wide default wording for the contributors section and invite page.
/// A template can override each field. Bound from the Contributors section.
/// </summary>
public class ContributorsOptions
{
    public string? SectionHeading { get; set; }
    public string? SectionBody { get; set; }
    public string? InviteButtonText { get; set; }
    public string? PageHeading { get; set; }
    public string? Intro { get; set; }
    public string? SignInText { get; set; }
    public string? OnceJoinedText { get; set; }
    public string? AddLaterText { get; set; }
    public string? AddButtonText { get; set; }
    public string? ProceedButtonText { get; set; }
    public string? CurrentHeading { get; set; }
}
