using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Options;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services;

/// <summary>
/// Provides configurable terminology for the domain concept "Application"
/// by reading values from the ApplicationTerminology configuration section.
/// </summary>
public class ApplicationTerminologyProvider(
    IOptions<ApplicationTerminologyOptions> options) : IApplicationTerminologyProvider
{
    /// <inheritdoc />
    public string Singular => options.Value.Singular;

    /// <inheritdoc />
    public string SingularCapitalised => Capitalise(Singular);

    /// <inheritdoc />
    public string Plural => options.Value.Plural;

    /// <inheritdoc />
    public string PluralCapitalised => Capitalise(Plural);

    private static string Capitalise(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToUpper(value[0]) + value[1..];
}
