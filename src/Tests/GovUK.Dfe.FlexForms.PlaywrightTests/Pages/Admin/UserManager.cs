using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;

public sealed class UserManager(IPage page) : BasePage(page)
{
    public async Task GoToAsync() => await Page.GotoAsync("/admin/user-manager");

    public async Task OpenAddUserAsync()
    {
        await Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Add new user" }).ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync("/admin/user-manager/add");
    }

    public async Task AddUserAsync(string name, string email, string role, IEnumerable<string> formNames)
    {
        await Page.GetByLabel("Name", new PageGetByLabelOptions { Exact = true }).FillAsync(name);
        await Page.GetByLabel("Email address").FillAsync(email);
        await Page.GetByLabel("Role", new PageGetByLabelOptions { Exact = true }).SelectOptionAsync(role);

        foreach (var formName in formNames)
        {
            await GrantFormAccessAsync(formName);
        }

        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add user" }).ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync("/admin/user-manager");
        await ExpectUserAddedAsync(email, role);
    }

    public async Task GrantFormAccessAsync(string formName) =>
        await FormAccessCheckbox(formName).CheckAsync();

    public async Task RevokeFormAccessAsync(string formName) =>
        await FormAccessCheckbox(formName).UncheckAsync();

    public async Task EditUserAsync(
        string email,
        string? newRole = null,
        IEnumerable<string>? formNamesToGrant = null,
        IEnumerable<string>? formNamesToRevoke = null)
    {
        var grant = formNamesToGrant?.ToArray() ?? [];
        var revoke = formNamesToRevoke?.ToArray() ?? [];
        if (string.IsNullOrWhiteSpace(newRole) && grant.Length == 0 && revoke.Length == 0)
        {
            return;
        }

        await FilterUsersBySearchTermAsync(email);
        await UserSummaryCard(email)
            .GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Edit" })
            .ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(
            new Regex(@"/admin/user-manager/edit(?:\?.*)?$"));

        if (!string.IsNullOrWhiteSpace(newRole))
        {
            await ById("Role").SelectOptionAsync(newRole);
        }

        foreach (var formName in grant)
        {
            await GrantFormAccessAsync(formName);
        }

        foreach (var formName in revoke)
        {
            await RevokeFormAccessAsync(formName);
        }

        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save" }).ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new Regex(@"/admin/user-manager(?:\?|$)"));
        await Assertions.Expect(Page.GetByRole(AriaRole.Alert))
            .ToContainTextAsync("User role and form access updated.");
    }

    private ILocator FormAccessCheckbox(string formName) =>
        Page.Locator(".govuk-checkboxes__item")
            .Filter(new LocatorFilterOptions { HasText = formName })
            .GetByRole(AriaRole.Checkbox);

    public async Task RemoveUserFromTenantAsync(string email)
    {
        await FilterUsersBySearchTermAsync(email);

        var removeButton = UserSummaryCard(email)
            .GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Remove from tenant" });

        await AcceptDialogAsync(() => removeButton.ClickAsync());

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Assertions.Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("User removed from this tenant.");
    }

    public async Task FilterUsersBySearchTermAsync(string searchTerm)
    {
        await EnsureUserFiltersPanelOpenAsync();
        await Page.GetByTestId("search-term").FillAsync(searchTerm);
        await Page.GetByTestId("apply-filters").ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task EnsureUserFiltersPanelOpenAsync()
    {
        var filters = Page.Locator("details.user-manager__filters-container");
        if (!await filters.EvaluateAsync<bool>("element => element.open"))
        {
            await Page.GetByTestId("filter-users-button").ClickAsync();
        }
    }

    private ILocator UserSummaryCard(string email) =>
        Page.Locator(".govuk-summary-card").Filter(new LocatorFilterOptions { HasText = email });

    private async Task ExpectUserAddedAsync(string email, string role)
    {
        await Assertions.Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync(
            $"User {email} has been added with role {role}.");
    }
}