using GovUK.Dfe.FlexForms.Application.DisplayCopy;
using GovUK.Dfe.FlexForms.Application.Interfaces;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services;

/// <summary>
/// Provides configurable terminology for the domain concept "Application".
/// A template value wins, then the tenant ApplicationTerminology setting.
/// </summary>
public class ApplicationTerminologyProvider(ITemplateDisplayCopy displayCopy) : IApplicationTerminologyProvider
{
    /// <inheritdoc />
    public string Singular => displayCopy.Singular;

    /// <inheritdoc />
    public string SingularCapitalised => displayCopy.SingularCapitalised;

    /// <inheritdoc />
    public string Plural => displayCopy.Plural;

    /// <inheritdoc />
    public string PluralCapitalised => displayCopy.PluralCapitalised;
}
