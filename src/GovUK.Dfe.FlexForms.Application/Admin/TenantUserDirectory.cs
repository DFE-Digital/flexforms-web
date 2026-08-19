using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;

namespace GovUK.Dfe.FlexForms.Application.Admin;

internal static class TenantUserDirectory
{
    internal static async Task<TenantUserDto?> GetByIdAsync(
        IUsersClient usersClient,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var page = await usersClient.GetTenantUsersAsync(
            pageNumber: 1,
            pageSize: 1,
            userId: userId,
            email: null,
            cancellationToken);
        return page?.Items?.FirstOrDefault(u => u.UserId == userId);
    }

    internal static async Task<bool> EmailExistsAsync(
        IUsersClient usersClient,
        string email,
        CancellationToken cancellationToken)
    {
        var page = await usersClient.GetTenantUsersAsync(
            pageNumber: 1,
            pageSize: 1,
            userId: null,
            email: email.Trim(),
            cancellationToken);
        return page?.Items?.Any(u =>
            string.Equals(u.Email, email.Trim(), StringComparison.OrdinalIgnoreCase)) == true;
    }
}
