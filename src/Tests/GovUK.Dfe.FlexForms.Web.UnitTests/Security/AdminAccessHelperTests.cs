using System.Security.Claims;
using GovUK.Dfe.FlexForms.Web.Security;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Security;

/// <summary>
/// One theory per check, one row per kind of user. A row changing here is a change to who can do what.
/// </summary>
public sealed class AdminAccessHelperTests
{
    public enum Persona
    {
        Anonymous,
        User,
        Caseworker,
        TemplateManager,
        UserManager,
        Admin,
        SuperAdmin
    }

    /// <summary>The one template the template manager has been given access to.</summary>
    private static readonly Guid GrantedTemplate = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static readonly Guid OtherTemplate = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Theory]
    [InlineData(Persona.Anonymous, false)]
    [InlineData(Persona.User, false)]
    [InlineData(Persona.Caseworker, false)]
    [InlineData(Persona.TemplateManager, false)]
    [InlineData(Persona.UserManager, false)]
    [InlineData(Persona.Admin, false)]
    [InlineData(Persona.SuperAdmin, true)]
    public void IsSuperAdmin(Persona persona, bool expected) =>
        Assert.Equal(expected, AdminAccessHelper.IsSuperAdmin(For(persona)));

    [Theory]
    [InlineData(Persona.Anonymous, false)]
    [InlineData(Persona.User, false)]
    [InlineData(Persona.Caseworker, false)]
    [InlineData(Persona.TemplateManager, false)]
    [InlineData(Persona.UserManager, false)]
    [InlineData(Persona.Admin, true)]
    [InlineData(Persona.SuperAdmin, true)]
    public void IsAdmin(Persona persona, bool expected) =>
        Assert.Equal(expected, AdminAccessHelper.IsAdmin(For(persona)));

    [Theory]
    [InlineData(Persona.Anonymous, false)]
    [InlineData(Persona.User, false)]
    [InlineData(Persona.Caseworker, false)]
    [InlineData(Persona.TemplateManager, true)]
    [InlineData(Persona.UserManager, true)]
    [InlineData(Persona.Admin, true)]
    [InlineData(Persona.SuperAdmin, true)]
    public void CanAccessAdminArea(Persona persona, bool expected) =>
        Assert.Equal(expected, AdminAccessHelper.CanAccessAdminArea(For(persona)));

    [Theory]
    [InlineData(Persona.Anonymous, false)]
    [InlineData(Persona.User, false)]
    [InlineData(Persona.Caseworker, false)]
    [InlineData(Persona.TemplateManager, true)]
    [InlineData(Persona.UserManager, false)]
    [InlineData(Persona.Admin, true)]
    [InlineData(Persona.SuperAdmin, true)]
    public void CanManageTemplates(Persona persona, bool expected) =>
        Assert.Equal(expected, AdminAccessHelper.CanManageTemplates(For(persona)));

    [Theory]
    [InlineData(Persona.Anonymous, false)]
    [InlineData(Persona.User, false)]
    [InlineData(Persona.Caseworker, false)]
    [InlineData(Persona.TemplateManager, false)]
    [InlineData(Persona.UserManager, true)]
    [InlineData(Persona.Admin, true)]
    [InlineData(Persona.SuperAdmin, true)]
    public void CanManageUsers(Persona persona, bool expected) =>
        Assert.Equal(expected, AdminAccessHelper.CanManageUsers(For(persona)));

    [Theory]
    [InlineData(Persona.Anonymous, false)]
    [InlineData(Persona.User, false)]
    [InlineData(Persona.Caseworker, true)]
    [InlineData(Persona.TemplateManager, false)]
    [InlineData(Persona.UserManager, false)]
    [InlineData(Persona.Admin, true)]
    [InlineData(Persona.SuperAdmin, true)]
    public void CanReadAnyApplication(Persona persona, bool expected) =>
        Assert.Equal(expected, AdminAccessHelper.CanReadAnyApplication(For(persona)));

    [Theory]
    [InlineData(Persona.Anonymous, false)]
    [InlineData(Persona.User, false)]
    [InlineData(Persona.Caseworker, false)]
    [InlineData(Persona.TemplateManager, false)]
    [InlineData(Persona.UserManager, false)]
    [InlineData(Persona.Admin, true)]
    [InlineData(Persona.SuperAdmin, true)]
    public void AdminOnlyChecks(Persona persona, bool expected)
    {
        var user = For(persona);

        Assert.Equal(expected, AdminAccessHelper.CanManageRoles(user));
        Assert.Equal(expected, AdminAccessHelper.CanManageOrganisationSettings(user));
        Assert.Equal(expected, AdminAccessHelper.CanManageEventMappings(user));
        Assert.Equal(expected, AdminAccessHelper.CanManageTenantSettings(user));
    }

    [Theory]
    [InlineData(Persona.Anonymous, false)]
    [InlineData(Persona.User, false)]
    [InlineData(Persona.Caseworker, false)]
    [InlineData(Persona.TemplateManager, false)]
    [InlineData(Persona.UserManager, false)]
    [InlineData(Persona.Admin, false)]
    [InlineData(Persona.SuperAdmin, true)]
    public void CanManagePlatformTenants(Persona persona, bool expected) =>
        Assert.Equal(expected, AdminAccessHelper.CanManagePlatformTenants(For(persona)));

    [Theory]
    [InlineData(Persona.Anonymous, false)]
    [InlineData(Persona.User, false)]
    [InlineData(Persona.Caseworker, false)]
    [InlineData(Persona.TemplateManager, false)]
    [InlineData(Persona.UserManager, false)]
    [InlineData(Persona.Admin, true)]
    [InlineData(Persona.SuperAdmin, false)]
    public void CanViewTenantConfigurationSummary(Persona persona, bool expected) =>
        Assert.Equal(expected, AdminAccessHelper.CanViewTenantConfigurationSummary(For(persona)));

    [Theory]
    [InlineData(Persona.Anonymous, false, false)]
    [InlineData(Persona.User, true, true)] // sees only what the API filtered for them
    [InlineData(Persona.Caseworker, true, true)]
    [InlineData(Persona.TemplateManager, true, false)] // sees every template, needs a grant to work in one
    [InlineData(Persona.UserManager, true, true)]
    [InlineData(Persona.Admin, true, true)]
    [InlineData(Persona.SuperAdmin, true, true)]
    public void HasTemplateAccess(Persona persona, bool granted, bool other)
    {
        var user = For(persona);

        Assert.Equal(granted, AdminAccessHelper.HasTemplateAccess(user, GrantedTemplate));
        Assert.Equal(other, AdminAccessHelper.HasTemplateAccess(user, OtherTemplate));
    }

    [Theory]
    [InlineData(Persona.Anonymous, false)]
    [InlineData(Persona.User, true)]
    [InlineData(Persona.Caseworker, true)]
    [InlineData(Persona.TemplateManager, false)]
    [InlineData(Persona.UserManager, true)]
    [InlineData(Persona.Admin, true)]
    [InlineData(Persona.SuperAdmin, true)]
    public void CanOpenTemplate_WithoutAGrantOnIt(Persona persona, bool expected) =>
        Assert.Equal(expected, AdminAccessHelper.CanOpenTemplate(For(persona), OtherTemplate));

    [Fact]
    public void TemplateManagerWhoCanReadEveryApplication_CanOpenATemplateTheyHaveNoGrantOn()
    {
        var user = Build(roles: [], "Template:Any:Manage", AdminAccessHelper.ApplicationAnyReadClaim);

        Assert.False(AdminAccessHelper.HasTemplateAccess(user, OtherTemplate));
        Assert.True(AdminAccessHelper.CanOpenTemplate(user, OtherTemplate));
    }

    [Theory]
    [InlineData("Template:Any:Read")]
    [InlineData("Template:Any:Write")]
    [InlineData("Template:bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb:Write")]
    public void TemplateManager_GetsTemplateAccessFromAReadOrWriteGrant(string grant)
    {
        var user = Build(roles: [], "Template:Any:Manage", grant);

        Assert.True(AdminAccessHelper.HasTemplateAccess(user, OtherTemplate));
    }

    [Fact]
    public void TemplateManagerOfOneTemplate_IsStillATemplateManager()
    {
        var user = Build(roles: [], $"Template:{GrantedTemplate}:Manage");

        Assert.True(AdminAccessHelper.CanManageTemplates(user));
        Assert.True(AdminAccessHelper.CanAccessAdminArea(user));
        // Manage on its own is not a grant to work in the template.
        Assert.False(AdminAccessHelper.HasTemplateAccess(user, GrantedTemplate));
    }

    [Fact]
    public void HasTemplateAccess_IsFalseForAnEmptyTemplateId()
    {
        Assert.False(AdminAccessHelper.HasTemplateAccess(For(Persona.User), Guid.Empty));
        Assert.True(AdminAccessHelper.HasTemplateAccess(For(Persona.Admin), Guid.Empty));
    }

    [Theory]
    [InlineData("Notifications:Any:Read", "Read", true)]
    [InlineData("Notifications:11111111-1111-1111-1111-111111111111:user@example.com:Read", "Read", true)]
    [InlineData("Notifications:Any:Read", "Write", false)]
    [InlineData("Template:Any:Read", "Read", false)]
    public void HasNotificationAccess(string grant, string accessType, bool expected) =>
        Assert.Equal(expected, AdminAccessHelper.HasNotificationAccess(Build(roles: [], grant), accessType));

    [Fact]
    public void HasNotificationAccess_AdminAlwaysAndAnonymousNever()
    {
        Assert.True(AdminAccessHelper.HasNotificationAccess(For(Persona.Admin), "Write"));
        Assert.False(AdminAccessHelper.HasNotificationAccess(null, "Read"));
        Assert.False(AdminAccessHelper.HasNotificationAccess(Build(roles: [], "Notifications:Any:Read"), " "));
    }

    [Fact]
    public void PermissionClaims_MatchIgnoringCase_AndOnlyFromThePermissionClaimType()
    {
        var lowerCase = Build(roles: [], "application:any:read");
        var wrongType = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("role", "Application:Any:Read"), new Claim("scope", "User:Any:Manage")], "Test"));

        Assert.True(AdminAccessHelper.CanReadAnyApplication(lowerCase));
        Assert.False(AdminAccessHelper.CanReadAnyApplication(wrongType));
        Assert.False(AdminAccessHelper.CanManageUsers(wrongType));
        Assert.False(AdminAccessHelper.HasAnyManageClaim(For(Persona.UserManager), " "));
    }

    [Fact]
    public void UserManagerAssignableRoles_OfferAdminOnlyToASuperAdmin()
    {
        (string Name, bool IsSystem)[] roles = [("Admin", true), ("User", true), ("SuperAdmin", true), ("Caseworker", false)];

        Assert.Equal(["Caseworker", "User"], AdminAccessHelper.GetUserManagerAssignableRoles(For(Persona.Admin), roles));
        Assert.Equal(["Caseworker", "User"], AdminAccessHelper.GetUserManagerAssignableRoles(For(Persona.UserManager), roles));
        Assert.Equal(["Admin", "Caseworker", "User"], AdminAccessHelper.GetUserManagerAssignableRoles(For(Persona.SuperAdmin), roles));
    }

    [Fact]
    public void UserManagerAssignableRoles_AddUserAndAdminWhenTheyAreNotListed()
    {
        Assert.Equal(["User"], AdminAccessHelper.GetUserManagerAssignableRoles(For(Persona.Admin), null));
        Assert.Equal(["Admin", "User"], AdminAccessHelper.GetUserManagerAssignableRoles(For(Persona.SuperAdmin), []));
    }

    private static ClaimsPrincipal? For(Persona persona) => persona switch
    {
        Persona.Anonymous => null,
        Persona.User => Build(["User"]),
        Persona.Caseworker => Build(["Caseworker"], AdminAccessHelper.ApplicationAnyReadClaim),
        Persona.TemplateManager => Build(["Template Manager"], AdminAccessHelper.TemplateManageAnyClaim, $"Template:{GrantedTemplate}:Read"),
        Persona.UserManager => Build(["User Manager"], "User:Any:Manage"),
        Persona.Admin => Build(["Admin"]),
        Persona.SuperAdmin => Build(["SuperAdmin"]),
        _ => throw new ArgumentOutOfRangeException(nameof(persona))
    };

    private static ClaimsPrincipal Build(string[] roles, params string[] permissions) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
                .. roles.Select(r => new Claim(ClaimTypes.Role, r)),
                .. permissions.Select(p => new Claim(AdminAccessHelper.PermissionClaimType, p))
            ],
            "Test"));
}
