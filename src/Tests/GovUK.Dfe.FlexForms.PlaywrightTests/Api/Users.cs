using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Api;

public static class Users
{
    public static async Task AddUserToRoleAsync(IAPIRequestContext request, string email, string name, string role,
        string templateName)
    {
        var templateId = await Templates.GetTemplateIdByNameAsync(request, templateName);

        var templateIds = new[] { templateId };
        var body = new CreateUserRoleRequest(email, name, role, templateIds);
        await AssignUserRoleAsync(request, body);
    }

    public static Task<UserDto> AssignUserRoleAsync(IAPIRequestContext request,
        CreateUserRoleRequest body)
        =>
            ApiBase.ApiRequestAsync<UserDto>(request, "/v1/users/roles", method: "POST", data: body);

    public static async Task<string?> GetTenantUserIdByEmailAsync(IAPIRequestContext request, string email)
    {
        var path = $"/v1/users/tenant?email={Uri.EscapeDataString(email.Trim())}";
        var result = await ApiBase.ApiRequestAsync<TenantUsersResult>(request, path);

        return result.Items?.FirstOrDefault()?.UserId;
    }

    public static async Task RemoveUserFromTenantAsync(IAPIRequestContext request, string email)
    {
        var userId = await GetTenantUserIdByEmailAsync(request, email);
        if (userId is null)
        {
            return;
        }

        await DeleteUserAsync(request, userId);
    }

    public static async Task DeleteUserAsync(IAPIRequestContext request, string userId)
    {
        var response = await request.FetchAsync(
            $"/v1/users/{userId}/tenant",
            new APIRequestContextOptions { Method = "DELETE" });

        if (response.Ok || response.Status == 404)
        {
            return;
        }

        throw new ApiRequestException($"API request failed ({response.Status}): {await response.TextAsync()}");
    }
}