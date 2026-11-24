using System.Security.Claims;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.CoreLibs.Security.Authorization;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

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

    [Fact]
    public async Task RefreshAsync_ReturnsTheCachedCopy_UnlessForced()
    {
        var user = SignedInUser();
        var cached = new UserAuthorizationDto { Roles = ["Cached"], Permissions = [] };
        var fresh = new UserAuthorizationDto { Roles = ["Fresh"], Permissions = [] };
        _cache.Set(UserPermissionsCache.GetCacheKey(user), cached);
        var client = Substitute.For<IUsersClient>();
        client.GetMyPermissionsAsync(Arg.Any<CancellationToken>()).Returns(fresh);

        Assert.Same(cached, await UserPermissionsCache.RefreshAsync(_cache, client, user));
        await client.DidNotReceiveWithAnyArgs().GetMyPermissionsAsync(default);

        Assert.Same(fresh, await UserPermissionsCache.RefreshAsync(_cache, client, user, forceRefresh: true));
        Assert.Same(fresh, _cache.Get(UserPermissionsCache.GetCacheKey(user)));
    }

    [Fact]
    public async Task RefreshAsync_ReturnsNull_ForASignedOutUserOrOneWithNoUserId()
    {
        var client = Substitute.For<IUsersClient>();
        var signedOut = new ClaimsPrincipal(new ClaimsIdentity());
        var noUserId = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, "a@b.c")], "Test"));

        Assert.Null(await UserPermissionsCache.RefreshAsync(_cache, client, signedOut));
        Assert.Null(await UserPermissionsCache.RefreshAsync(_cache, client, noUserId));
        await client.DidNotReceiveWithAnyArgs().GetMyPermissionsAsync(default);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task RefreshAsync_RethrowsAuthFailures_SoTheMiddlewareCanLogOut(int status)
    {
        var client = Substitute.For<IUsersClient>();
        client.GetMyPermissionsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new ExternalApplicationsException("denied", status, "", new Dictionary<string, IEnumerable<string>>(), null));

        await Assert.ThrowsAsync<ExternalApplicationsException>(
            () => UserPermissionsCache.RefreshAsync(_cache, client, SignedInUser()));
    }

    [Fact]
    public async Task RefreshAsync_ReturnsNull_WhenTheRequestIsCancelled()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var client = Substitute.For<IUsersClient>();
        client.GetMyPermissionsAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new OperationCanceledException());

        Assert.Null(await UserPermissionsCache.RefreshAsync(_cache, client, SignedInUser(), cancellationToken: cancelled.Token));
    }

    [Fact]
    public void GetCacheKey_IsSeparatePerTenant()
    {
        var tenantA = Guid.NewGuid();
        var withClaim = SignedInUser(new Claim("tenant_id", "from-claim"));

        Assert.NotEqual(
            UserPermissionsCache.GetCacheKey(SignedInUser(), tenantA),
            UserPermissionsCache.GetCacheKey(SignedInUser(), Guid.NewGuid()));
        Assert.Contains(tenantA.ToString("N"), UserPermissionsCache.GetCacheKey(withClaim, tenantA));
        Assert.Contains("from-claim", UserPermissionsCache.GetCacheKey(withClaim));
        Assert.Contains("_none_", UserPermissionsCache.GetCacheKey(SignedInUser()));
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
