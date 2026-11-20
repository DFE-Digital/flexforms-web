using System.Security.Claims;
using GovUK.Dfe.FlexForms.Web.Security;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Security;

public sealed class ApplicationPermissionHelperTests
{
    private static readonly Guid ApplicationId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherApplicationId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid TemplateId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Theory]
    [InlineData("Admin", null, true)]
    [InlineData("SuperAdmin", null, true)]
    [InlineData("User", "Application:aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa:Write", true)]
    [InlineData("User", "application:AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA:write", true)]
    [InlineData("User", "Application:Any:Write", false)] // tenant-wide Write is not a grant on this application
    [InlineData("User", "Application:aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa:Read", false)]
    [InlineData("User", "Application:bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb:Write", false)]
    [InlineData("Caseworker", "Application:Any:Read", false)]
    [InlineData("User", null, false)]
    public void CanWriteApplication(string role, string? grant, bool expected) =>
        Assert.Equal(expected, ApplicationPermissionHelper.CanWriteApplication(SignedIn(role, grant), ApplicationId));

    [Theory]
    [InlineData("Admin", null, true)]
    [InlineData("SuperAdmin", null, true)]
    [InlineData("User", "Application:aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa:Read", true)]
    [InlineData("Caseworker", "Application:Any:Read", true)]
    [InlineData("User", "Application:bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb:Read", false)]
    [InlineData("User", "Template:cccccccc-cccc-cccc-cccc-cccccccccccc:Read", false)]
    [InlineData("User", null, false)]
    public void CanReadApplication(string role, string? grant, bool expected) =>
        Assert.Equal(expected, ApplicationPermissionHelper.CanReadApplication(SignedIn(role, grant), ApplicationId));

    [Theory]
    [InlineData("Admin", null, true)]
    [InlineData("SuperAdmin", null, true)]
    [InlineData("User", "Template:cccccccc-cccc-cccc-cccc-cccccccccccc:Write", true)]
    [InlineData("User", "Template:Any:Write", true)]
    [InlineData("User", "Template:cccccccc-cccc-cccc-cccc-cccccccccccc:Read", false)] // invited contributors
    [InlineData("Template Manager", "Template:cccccccc-cccc-cccc-cccc-cccccccccccc:Manage", false)]
    [InlineData("Template Manager", "Template:Any:Manage", false)]
    [InlineData("User", null, false)]
    public void CanCreateApplicationsForTemplate(string role, string? grant, bool expected) =>
        Assert.Equal(expected, ApplicationPermissionHelper.CanCreateApplicationsForTemplate(SignedIn(role, grant), TemplateId));

    [Fact]
    public void CanCreateApplicationsForTemplate_IsFalseForAnEmptyTemplateIdEvenForAnAdmin() =>
        Assert.False(ApplicationPermissionHelper.CanCreateApplicationsForTemplate(SignedIn("Admin", null), Guid.Empty));

    [Fact]
    public void SignedOutUser_CanDoNothing_EvenWithAnAdminRoleOrGrants()
    {
        var signedOut = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim("permission", $"Application:{ApplicationId}:Write"),
                new Claim("permission", $"Application:{ApplicationId}:Read"),
                new Claim("permission", $"Template:{TemplateId}:Write")
            ]));

        Assert.False(ApplicationPermissionHelper.CanWriteApplication(signedOut, ApplicationId));
        Assert.False(ApplicationPermissionHelper.CanReadApplication(signedOut, ApplicationId));
        Assert.False(ApplicationPermissionHelper.CanCreateApplicationsForTemplate(signedOut, TemplateId));
        Assert.False(ApplicationPermissionHelper.CanWriteApplication(null, ApplicationId));
        Assert.False(ApplicationPermissionHelper.CanReadApplication(null, ApplicationId));
        Assert.False(ApplicationPermissionHelper.CanCreateApplicationsForTemplate(null, TemplateId));
    }

    [Fact]
    public void GrantsCountOnlyFromThePermissionClaimType()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("scope", $"Application:{ApplicationId}:Write"), new Claim("scope", "Application:Any:Read")],
            "Test"));

        Assert.False(ApplicationPermissionHelper.CanWriteApplication(user, ApplicationId));
        Assert.False(ApplicationPermissionHelper.CanReadApplication(user, OtherApplicationId));
    }

    private static ClaimsPrincipal SignedIn(string role, string? grant)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "user-1"), new(ClaimTypes.Role, role) };
        if (grant is not null)
            claims.Add(new Claim("permission", grant));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }
}
