using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Domain.FormEngine;

/// <summary>
/// Resolves post-save navigation mode from page settings, with backward-compatible fallbacks.
/// </summary>
public static class PageNavigationPolicy
{
    /// <summary>
    /// Prefer <see cref="Page.NavigationAfterSave"/> when set; otherwise map
    /// <see cref="Page.ReturnToSummaryPage"/> (<c>true</c> → summary, <c>false</c> → linear).
    /// </summary>
    public static NavigationAfterSave Resolve(Page page)
    {
        if (page.NavigationAfterSave.HasValue)
            return page.NavigationAfterSave.Value;

        return page.ReturnToSummaryPage
            ? NavigationAfterSave.Summary
            : NavigationAfterSave.Linear;
    }
}
