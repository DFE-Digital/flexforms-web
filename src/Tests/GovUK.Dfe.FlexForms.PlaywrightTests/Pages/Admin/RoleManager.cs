using System.Text.RegularExpressions;
using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;

public sealed class RoleManager(IPage page, Terminology terminology) : BasePage(page, terminology)
{
    public async Task GoToAsync() => await Page.GotoAsync("/admin/role-manager");

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

        await AcceptDialogAsync(() => deleteButton.ClickAsync());

        await Assertions.Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("Role deleted.");
    }

    public async Task ManageRolePermissionsAsync(string roleName)
    {
        await RoleRow(roleName)
            .GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Manage permissions" }).ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(
            new Regex(@"/admin/role-manager/permissions(?:\?.*)?$"));
    }

    private ILocator RoleRow(string roleName) =>
        Page.Locator("tbody.govuk-table__body > tr.govuk-table__row")
            .Filter(new LocatorFilterOptions
            {
                Has = Page.GetByRole(AriaRole.Textbox, new PageGetByRoleOptions { Name = $"Rename {roleName}" }),
            });
}