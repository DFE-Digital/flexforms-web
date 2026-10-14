using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Api;

public static class Roles
{
    public static Task<TenantRoleDto> CreateRoleAsync(IAPIRequestContext request, string roleName) =>
        ApiBase.ApiRequestAsync<TenantRoleDto>(request, "/v1/roles", method: "POST", data: new { Name = roleName });

    public static Task<IReadOnlyList<TenantRoleDto>> GetRolesAsync(IAPIRequestContext request) =>
        ApiBase.ApiRequestAsync<IReadOnlyList<TenantRoleDto>>(request, "/v1/roles");

    public static async Task<string?> GetRoleIdByNameAsync(IAPIRequestContext request, string roleName)
    {
        var roles = await GetRolesAsync(request);

        return roles.FirstOrDefault(role =>
            string.Equals(role.Name, roleName.Trim(), StringComparison.OrdinalIgnoreCase))?.RoleId;
    }

    public static async Task RemoveRoleAsync(IAPIRequestContext request, string roleName)
    {
        var roleId = await GetRoleIdByNameAsync(request, roleName);
        if (roleId is null)
        {
            return;
        }

        await DeleteRoleAsync(request, roleId);
    }

    public static Task DeleteRoleAsync(IAPIRequestContext request, string roleId) =>
        ApiBase.ApiRequestAsync(
            request,
            $"/v1/roles/{roleId}",
            method: "DELETE",
            allowNotFound: true);
}
