using Microsoft.Extensions.Configuration;

namespace GovUK.Dfe.FlexForms.Application.Interfaces;

/// <summary>
/// Provides the effective application configuration for the current request,
/// preferring tenant settings over host settings when platform bootstrap is active.
/// </summary>
public interface IRequestAppConfiguration
{
    /// <summary>
    /// Gets a configuration value by key.
    /// </summary>
    string? this[string key] { get; }

    /// <summary>
    /// Gets a configuration section by key.
    /// </summary>
    IConfigurationSection GetSection(string key);

    /// <summary>
    /// Gets the effective configuration root for binding and <c>GetValue</c> helpers.
    /// </summary>
    IConfiguration Current { get; }
}
