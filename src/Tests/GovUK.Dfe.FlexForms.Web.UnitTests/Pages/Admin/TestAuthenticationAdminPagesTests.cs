using GovUK.Dfe.CoreLibs.Security.Configurations;
using GovUK.Dfe.FlexForms.Api.Client.Security;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Web.Pages.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages.Admin;

/// <summary>
/// The User Manager's authenticator code page and the sign-in lock on the edit page exist only where
/// Test Authentication is active: an allowed environment and a tenant that turns it on.
/// </summary>
public class TestAuthenticationAdminPagesTests
{
    private static readonly Guid UserId = Guid.Parse("44444444-0000-4000-8000-000000000001");

    private readonly ITestAuthAuthenticatorCodeAdmin _codeAdmin = Substitute.For<ITestAuthAuthenticatorCodeAdmin>();
    private readonly IUserManagerEditAdmin _editAdmin = Substitute.For<IUserManagerEditAdmin>();
    private readonly ITestAuthSignInLockAdmin _lockAdmin = Substitute.For<ITestAuthSignInLockAdmin>();
    private readonly IOptions<TestAuthenticationOptions> _testAuthOptions = Options.Create(new TestAuthenticationOptions());
    private readonly IOptions<EntraSsoOptions> _entraOptions = Options.Create(new EntraSsoOptions());

    /// <summary>Environment and tenant setting pairs where Test Authentication is not active.</summary>
    public static readonly TheoryData<string, bool?> TestAuthOff = new()
    {
        { "Production", true },
        { "Prod", true },
        { "Staging", true },
        { "Development", false },
        { "Development", null },
        { "Test", false },
    };

    public TestAuthenticationAdminPagesTests()
    {
        _editAdmin.LoadAsync(default!, default).ReturnsForAnyArgs(AdminPageOutcome.Stay());
        _codeAdmin.CreateAsync(default!, default).ReturnsForAnyArgs(AdminPageOutcome.Stay());
        _lockAdmin.SetLockAsync(default!, default).ReturnsForAnyArgs(AdminPageOutcome.Redirect());
        _lockAdmin.ClearTemporaryLockoutAsync(default!, default).ReturnsForAnyArgs(AdminPageOutcome.Redirect());
    }

    [Theory]
    [MemberData(nameof(TestAuthOff))]
    public async Task AuthenticatorCodePage_ShouldBeNotFound_WhenTestAuthenticationIsOff(string environment, bool? tenantEnables)
    {
        var page = CodePage(environment, tenantEnables);

        Assert.IsType<NotFoundResult>(page.OnGet());
        Assert.IsType<NotFoundResult>(await page.OnPostAsync(CancellationToken.None));
        Assert.Empty(_codeAdmin.ReceivedCalls());
    }

    [Fact]
    public async Task AuthenticatorCodePage_ShouldCreateACode_WhenTestAuthenticationIsActive()
    {
        var expires = new DateTime(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
        _codeAdmin.CreateAsync(Arg.Any<TestAuthAuthenticatorCodeWorkState>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var state = call.Arg<TestAuthAuthenticatorCodeWorkState>();
                state.Code = "ABCD-1234";
                state.ExpiresAtUtc = expires;
                return AdminPageOutcome.Stay(successMessage: "Code created");
            });
        var page = CodePage("Development", tenantEnables: true);
        page.Email = "new.user@tenant.example";

        Assert.IsType<PageResult>(page.OnGet());
        Assert.IsType<PageResult>(await page.OnPostAsync(CancellationToken.None));

        await _codeAdmin.Received(1).CreateAsync(
            Arg.Is<TestAuthAuthenticatorCodeWorkState>(state => state.Email == "new.user@tenant.example"),
            Arg.Any<CancellationToken>());
        Assert.Equal("ABCD-1234", page.Code);
        Assert.Equal(expires, page.ExpiresAtUtc);
        Assert.Equal("Code created", page.SuccessMessage);
    }

    [Fact]
    public async Task AuthenticatorCodePage_ShouldShowTheErrors_WhenTheCodeCannotBeCreated()
    {
        _codeAdmin.CreateAsync(default!, default).ReturnsForAnyArgs(AdminPageOutcome.Stay(
            errorMessage: "Could not create a code",
            errors: [new("Email", "Enter an email address")]));
        var page = CodePage("Local", tenantEnables: true);

        await page.OnPostAsync(CancellationToken.None);

        Assert.Equal("Could not create a code", page.ErrorMessage);
        Assert.Contains(page.ModelState["Email"]!.Errors, error => error.ErrorMessage == "Enter an email address");
    }

    [Theory]
    [MemberData(nameof(TestAuthOff))]
    public async Task EditPage_ShouldHideTheSignInLockAndRefuseItsHandlers_WhenTestAuthenticationIsOff(string environment, bool? tenantEnables)
    {
        var page = EditPage(environment, tenantEnables);

        Assert.IsType<PageResult>(await page.OnGetAsync(CancellationToken.None));
        Assert.Null(page.SignInLock);
        Assert.IsType<NotFoundResult>(await page.OnPostSignInLockAsync(CancellationToken.None));
        Assert.IsType<NotFoundResult>(await page.OnPostClearTemporaryLockoutAsync(CancellationToken.None));

        Assert.Empty(_lockAdmin.ReceivedCalls());
    }

    [Fact]
    public async Task EditPage_ShouldShowAndChangeTheSignInLock_WhenTestAuthenticationIsActive()
    {
        _lockAdmin.LoadAsync(Arg.Any<TestAuthSignInLockWorkState>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                call.Arg<TestAuthSignInLockWorkState>().LockSignIn = true;
                return (string?)null;
            });
        _lockAdmin.SetLockAsync(default!, default).ReturnsForAnyArgs(AdminPageOutcome.Redirect(successMessage: "Locked"));
        var page = EditPage("Development", tenantEnables: true);

        await page.OnGetAsync(CancellationToken.None);

        Assert.NotNull(page.SignInLock);
        Assert.True(page.LockSignIn);

        page.LockSignIn = false;
        var result = await page.OnPostSignInLockAsync(CancellationToken.None);
        await page.OnPostClearTemporaryLockoutAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Admin/UserManagerEdit", redirect.PageName);
        Assert.Equal(UserId, redirect.RouteValues!["userId"]);
        await _lockAdmin.Received(1).SetLockAsync(
            Arg.Is<TestAuthSignInLockWorkState>(state => state.UserId == UserId && !state.LockSignIn),
            Arg.Any<CancellationToken>());
        await _lockAdmin.Received(1).ClearTemporaryLockoutAsync(
            Arg.Is<TestAuthSignInLockWorkState>(state => state.UserId == UserId),
            Arg.Any<CancellationToken>());
        Assert.Equal("Locked", page.TempData[UserManagerEditModel.SignInLockSuccessKey]);
    }

    private UserManagerAuthenticatorCodeModel CodePage(string environment, bool? tenantEnables)
    {
        var page = new UserManagerAuthenticatorCodeModel(_codeAdmin, _testAuthOptions, _entraOptions);
        PageModelTestContext.Attach(page, TestPrincipals.UserManager(), PageModelTestContext.TestAuthServices(environment, tenantEnables));
        return page;
    }

    private UserManagerEditModel EditPage(string environment, bool? tenantEnables)
    {
        var page = new UserManagerEditModel(
            _editAdmin,
            _lockAdmin,
            Substitute.For<IInternalUserTokenStore>(),
            new MemoryCache(new MemoryCacheOptions()),
            _testAuthOptions,
            _entraOptions)
        {
            UserId = UserId
        };
        PageModelTestContext.Attach(page, TestPrincipals.UserManager(), PageModelTestContext.TestAuthServices(environment, tenantEnables));
        return page;
    }
}
