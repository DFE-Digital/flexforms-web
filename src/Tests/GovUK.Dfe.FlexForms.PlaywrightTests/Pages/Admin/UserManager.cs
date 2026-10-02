using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;

public sealed class UserManager(IPage page, Terminology terminology) : BasePage(page, terminology)
{
    public async Task OpenAddUserAsync()
    {
        await Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Add new user" }).ClickAsync();
        await Page.WaitForURLAsync("**/admin/user-manager/add");
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
        await Page.WaitForURLAsync("**/admin/user-manager");
        await ExpectUserAddedAsync(email, role);
    }

    public async Task GrantFormAccessAsync(string formName)
    {
        var item = Page.Locator(".govuk-checkboxes__item").Filter(new LocatorFilterOptions { HasText = formName });
        await item.GetByRole(AriaRole.Checkbox).CheckAsync();
    }

    public async Task RemoveUserFromTenantAsync(string email)
    {
        await FilterUsersBySearchTermAsync(email);

        var removeButton = UserSummaryCard(email)
            .GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Remove from tenant" });

        Page.Dialog += AcceptRemoveConfirmDialogAsync;
        try
        {
            await removeButton.ClickAsync();
        }
        finally
        {
            Page.Dialog -= AcceptRemoveConfirmDialogAsync;
        }

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

    private async void AcceptRemoveConfirmDialogAsync(object? _, IDialog dialog)
    {
        Page.Dialog -= AcceptRemoveConfirmDialogAsync;
        await dialog.AcceptAsync();
    }

    private async Task ExpectUserAddedAsync(string email, string role)
    {
        await Assertions.Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync(
            $"User {email} has been added with role {role}.");
    }
}
