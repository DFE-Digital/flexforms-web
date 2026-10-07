using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;

public sealed class RoleManager(IPage page, Terminology terminology) : BasePage(page, terminology)
{
    public async Task CreateRoleAsync(string roleName)
    {
        await ById("NewRoleName").FillAsync(roleName);
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create role" }).ClickAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Alert))
            .ToContainTextAsync($"Role '{roleName}' has been created.");
    }

    public async Task DeleteRoleAsync(string roleName)
    {
        var deleteButton = RoleRow(roleName)
            .GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete" });

        Page.Dialog += AcceptDeleteConfirmDialogAsync;
        try
        {
            await deleteButton.ClickAsync();
        }
        finally
        {
            Page.Dialog -= AcceptDeleteConfirmDialogAsync;
        }

        await Assertions.Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("Role deleted.");
    }

    public async Task ManageRolePermissionsAsync(string roleName)
    {
        await RoleRow(roleName)
            .GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Manage permissions" }).ClickAsync();
        await Page.WaitForURLAsync("**/admin/role-manager/permissions**");
    }

    private ILocator RoleRow(string roleName) =>
        Page.Locator("tbody.govuk-table__body > tr.govuk-table__row")
            .Filter(new LocatorFilterOptions
            {
                Has = Page.GetByRole(AriaRole.Textbox, new PageGetByRoleOptions { Name = $"Rename {roleName}" }),
            });

    private async void AcceptDeleteConfirmDialogAsync(object? _, IDialog dialog)
    {
        Page.Dialog -= AcceptDeleteConfirmDialogAsync;
        await dialog.AcceptAsync();
    }
}