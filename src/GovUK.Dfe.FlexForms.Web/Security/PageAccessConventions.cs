using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// The folder and page access rules for Razor Pages, in one place. Page attributes
/// (<c>[Authorize(Policy = …)]</c>, <c>[AllowAnonymous]</c>) add to these. The effective rule for every page
/// and controller action is pinned in the unit tests' <c>Security/ExpectedPageSecurity.json</c>.
/// </summary>
public static class PageAccessConventions
{
    /// <summary>Policy every page needs unless a rule below lets it through anonymously.</summary>
    public const string SignedInPolicy = "OpenIdConnectPolicy";

    public enum Access
    {
        /// <summary>Requires the rule's policy.</summary>
        Policy,

        /// <summary>No sign-in needed. Wins over any policy or attribute on the page.</summary>
        Anonymous
    }

    /// <param name="Path">Page path (<c>/Feedback/Index</c>) or, for a folder rule, folder path (<c>/</c>).</param>
    /// <param name="IsFolder">True when the rule applies to every page under <paramref name="Path"/>.</param>
    /// <param name="NonProductionOnly">Applied only in Development, Test and Staging.</param>
    public sealed record Rule(string Path, bool IsFolder, Access Access, string? Policy = null, bool NonProductionOnly = false);

    public static IReadOnlyList<Rule> Rules { get; } =
    [
        new("/", IsFolder: true, Access.Policy, SignedInPolicy),
        new("/Logout", IsFolder: false, Access.Anonymous),

        new("/Diagnostics", IsFolder: false, Access.Policy, AdminAccessHelper.CanManageTenantSettingsPolicy),

        // Feedback can be given without signing in.
        new("/Feedback/Index", IsFolder: false, Access.Anonymous),
        new("/Feedback/BugReport", IsFolder: false, Access.Anonymous),
        new("/Feedback/SupportRequest", IsFolder: false, Access.Anonymous),
        new("/Feedback/General", IsFolder: false, Access.Anonymous),
        new("/Feedback/ThankYou", IsFolder: false, Access.Anonymous),

        new("/Shared/Cookies", IsFolder: false, Access.Anonymous),

        new("/Error/NotFound", IsFolder: false, Access.Anonymous),
        new("/Error/Forbidden", IsFolder: false, Access.Anonymous),
        new("/Error/General", IsFolder: false, Access.Anonymous),
        new("/Error/ServerError", IsFolder: false, Access.Anonymous),

        new("/TestError", IsFolder: false, Access.Anonymous, NonProductionOnly: true),

        // Always anonymous; the pages return 404 unless the current tenant (or host) selects
        // Test Authentication via TenantAuthSchemeSelector.
        new("/TestLogin", IsFolder: false, Access.Anonymous),
        new("/TestLogout", IsFolder: false, Access.Anonymous),
    ];

    /// <summary>Development, Test and Staging: where <see cref="Rule.NonProductionOnly"/> rules apply.</summary>
    public static bool IsNonProduction(IHostEnvironment environment) =>
        environment.IsDevelopment() || environment.IsEnvironment("Test") || environment.IsStaging();

    public static void Apply(PageConventionCollection conventions, IHostEnvironment environment)
    {
        var nonProduction = IsNonProduction(environment);

        foreach (var rule in Rules)
        {
            if (rule.NonProductionOnly && !nonProduction)
                continue;

            switch (rule)
            {
                case { Access: Access.Anonymous, IsFolder: true }:
                    conventions.AllowAnonymousToFolder(rule.Path);
                    break;
                case { Access: Access.Anonymous }:
                    conventions.AllowAnonymousToPage(rule.Path);
                    break;
                case { IsFolder: true }:
                    conventions.AuthorizeFolder(rule.Path, RequirePolicy(rule));
                    break;
                default:
                    conventions.AuthorizePage(rule.Path, RequirePolicy(rule));
                    break;
            }
        }
    }

    private static string RequirePolicy(Rule rule) =>
        rule.Policy ?? throw new InvalidOperationException($"Access rule for '{rule.Path}' needs a policy.");
}
