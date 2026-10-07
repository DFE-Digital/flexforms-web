using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;

public sealed class ManagePermissions(IPage page, Terminology terminology) : BasePage(page, terminology)
{
    public async Task GoToAsync(Guid roleId) =>
        await Page.GotoAsync($"/admin/role-manager/permissions?roleId={roleId}");

    public async Task AddPermissionAsync(string resourceType, string resourceKey, string accessType)
    {
        await SelectResourceTypeAsync(resourceType);
        await SetResourceKeyAsync(resourceKey);
        await SelectAccessTypeAsync(accessType);
        await ClickAddPermissionButtonAsync();
    }

    private Task SelectResourceTypeAsync(string resourceType) =>
        ById("NewResourceType").SelectOptionAsync(resourceType);

    private Task SetResourceKeyAsync(string resourceKey) =>
        ById("NewResourceKey").FillAsync(resourceKey);

    private Task SelectAccessTypeAsync(string accessType) =>
        ById("NewAccessType").SelectOptionAsync(accessType);

    private Task ClickAddPermissionButtonAsync() =>
        Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add", Exact = true }).ClickAsync();
}