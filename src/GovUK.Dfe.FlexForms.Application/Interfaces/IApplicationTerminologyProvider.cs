namespace GovUK.Dfe.FlexForms.Application.Interfaces;

/// <summary>
/// Provides configurable terminology for the domain concept "Application",
/// allowing each service (e.g. Transfers, LSRP) to customise the display terms
/// used across the UI.
/// </summary>
public interface IApplicationTerminologyProvider
{
    /// <summary>
    /// The singular form in lowercase (e.g. "application", "reform plan").
    /// </summary>
    string Singular { get; }

    /// <summary>
    /// The singular form with the first letter capitalised (e.g. "Application", "Reform plan").
    /// </summary>
    string SingularCapitalised { get; }

    /// <summary>
    /// The plural form in lowercase (e.g. "applications", "reform plans").
    /// </summary>
    string Plural { get; }

    /// <summary>
    /// The plural form with the first letter capitalised (e.g. "Applications", "Reform plans").
    /// </summary>
    string PluralCapitalised { get; }
}
