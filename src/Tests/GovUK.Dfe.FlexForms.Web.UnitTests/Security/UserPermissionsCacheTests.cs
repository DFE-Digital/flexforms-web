using System.Security.Claims;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.CoreLibs.Security.Authorization;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Security;

public class UserPermissionsCacheTests
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly CustomClaimsTransformation _transformation;

    public UserPermissionsCacheTests()
    {
        _transformation = new CustomClaimsTransformation(
            [new PermissionsClaimProvider(_cache, new HttpContextAccessor())]);
    }

    [Fact]
    public async Task RevokedRole_IsGone_AfterClaimsAreRebuilt()
    {
        var user = SignedInUser();
        Cache(user, roles: ["Admin"]);
        await _transformation.TransformAsync(user);
        Assert.True(user.IsInRole("Admin"));

        Cache(user, roles: ["User"]);
        UserPermissionsCache.RemovePermissionClaims(user);
        await _transformation.TransformAsync(user);

        Assert.False(user.IsInRole("Admin"));
        Assert.True(user.IsInRole("User"));
    }

    [Fact]
    public void RemovePermissionClaims_KeepsRolesThatCameWithTheSignIn()
    {
        var user = SignedInUser(new Claim(ClaimTypes.Role, "SignInRole"));
        user.Identities.First().AddClaim(
            new Claim(ClaimTypes.Role, "Admin", ClaimValueTypes.String, UserPermissionsCache.ClaimIssuer));
        user.Identities.First().AddClaim(
            new Claim("permission", "Template:1:Read", ClaimValueTypes.String, UserPermissionsCache.ClaimIssuer));

        UserPermissionsCache.RemovePermissionClaims(user);

        Assert.True(user.IsInRole("SignInRole"));
        Assert.False(user.IsInRole("Admin"));
        Assert.DoesNotContain(user.Claims, claim => claim.Type == "permission");
    }

    [Fact]
    public async Task InviteScopedApplicationKeys_AreEmittedAsContributorInviteScopeClaims()
    {
        var user = SignedInUser();
        var applicationKey = Guid.NewGuid().ToString();
        _cache.Set(
            UserPermissionsCache.GetCacheKey(user),
            new UserAuthorizationDto
            {
                Roles = ["Caseworker"],
                Permissions =
                [
                    new UserPermissionDto
                    {
                        ResourceType = ResourceType.Application,
                        ResourceKey = applicationKey,
                        AccessType = AccessType.Read
                    },
                    new UserPermissionDto
                    {
                        ResourceType = ResourceType.ApplicationFiles,
                        ResourceKey = "Any",
                        AccessType = AccessType.Read
                    }
                ],
                InviteScopedApplicationKeys = [applicationKey]
            });

        await _transformation.TransformAsync(user);

        Assert.Contains(
            user.Claims,
            c => c.Type == "permission"
                 && c.Value == $"ContributorInvite:{applicationKey}:Scope"
                 && c.Issuer == UserPermissionsCache.ClaimIssuer);
        Assert.Contains(
            user.Claims,
            c => c.Type == "permission" && c.Value == $"ApplicationFiles:Any:Read");
    }

    private void Cache(ClaimsPrincipal user, string[] roles) =>
        _cache.Set(
            UserPermissionsCache.GetCacheKey(user),
            new UserAuthorizationDto { Roles = roles, Permissions = [] });

    private static ClaimsPrincipal SignedInUser(params Claim[] extraClaims) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-1"), new Claim(ClaimTypes.Email, "user@example.com"), .. extraClaims],
            "Test"));
}
