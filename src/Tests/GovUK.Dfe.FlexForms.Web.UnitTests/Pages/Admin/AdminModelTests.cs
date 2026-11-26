using System.Security.Claims;
using GovUK.Dfe.CoreLibs.Caching.Helpers;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Security;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Caching;
using GovUK.Dfe.FlexForms.Web.Pages.Admin;
using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Web.Services;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages.Admin;

public class AdminModelTests
{
    private static readonly Guid GivenTemplateId = Guid.Parse("22222222-0000-4000-8000-000000000001");
    private static readonly Guid OtherTemplateId = Guid.Parse("22222222-0000-4000-8000-000000000002");
    private static readonly Guid TenantId = Guid.Parse("22222222-0000-4000-8000-0000000000aa");

    private readonly IAdminHome _adminHome = Substitute.For<IAdminHome>();
    private readonly ITwoLevelCache _templateCache = Substitute.For<ITwoLevelCache>();
    private readonly ITemplateCacheInvalidator _cacheInvalidator = Substitute.For<ITemplateCacheInvalidator>();
    private readonly IHttpContextAccessor _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
    private readonly IInternalUserTokenStore _tokenStore = Substitute.For<IInternalUserTokenStore>();
    private readonly ITemplateSelectionService _selection = Substitute.For<ITemplateSelectionService>();
    private readonly IAuthenticationService _authentication = Substitute.For<IAuthenticationService>();
    private readonly ITenantRequestContext _tenant = new TenantRequestContext { TenantId = TenantId };
    private readonly AdminModel _model;
    private PageModelTestContext _context = null!;

    public AdminModelTests()
    {
        _model = new AdminModel(
            _adminHome,
            _templateCache,
            _cacheInvalidator,
            _httpContextAccessor,
            _tokenStore,
            _selection,
            _tenant,
            NullLogger<AdminModel>.Instance);
        SignIn(TestPrincipals.Admin());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OnPostMakeLiveOrNotLive_ShouldShowAnErrorAndCallNoApi_WhenTheManagerHasNoGrantOnTheTemplate(bool makeLive)
    {
        SignIn(TestPrincipals.TemplateManager(GivenTemplateId));

        var result = makeLive
            ? await _model.OnPostMakeLiveAsync(OtherTemplateId)
            : await _model.OnPostMakeNotLiveAsync(OtherTemplateId);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Null(redirect.PageName);
        Assert.Equal(AdminAccessHelper.NoTemplateAccessText, _context.TempData["AdminError"]);
        Assert.Null(_context.TempData["AdminSuccess"]);
        Assert.Empty(_adminHome.ReceivedCalls());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OnPostMakeLiveOrNotLive_ShouldChangeTheTemplate_WhenTheManagerWasGivenIt(bool makeLive)
    {
        SignIn(TestPrincipals.TemplateManager(GivenTemplateId));
        _adminHome.SetTemplateLiveAsync(GivenTemplateId, makeLive, Arg.Any<CancellationToken>())
            .Returns(AdminPageOutcome.Redirect(successMessage: "Changed", errorMessage: "Partly failed"));

        var result = makeLive
            ? await _model.OnPostMakeLiveAsync(GivenTemplateId)
            : await _model.OnPostMakeNotLiveAsync(GivenTemplateId);

        Assert.IsType<RedirectToPageResult>(result);
        await _adminHome.Received(1).SetTemplateLiveAsync(GivenTemplateId, makeLive, Arg.Any<CancellationToken>());
        Assert.Equal("Changed", _context.TempData["AdminSuccess"]);
        Assert.Equal("Partly failed", _context.TempData["AdminError"]);
    }

    [Fact]
    public async Task OnPostMakeLive_ShouldChangeAnyTemplate_ForAnAdmin()
    {
        _adminHome.SetTemplateLiveAsync(OtherTemplateId, true, Arg.Any<CancellationToken>())
            .Returns(AdminPageOutcome.Redirect(successMessage: "Live"));

        await _model.OnPostMakeLiveAsync(OtherTemplateId);

        await _adminHome.Received(1).SetTemplateLiveAsync(OtherTemplateId, true, Arg.Any<CancellationToken>());
        Assert.Null(_context.TempData["AdminError"]);
    }

    [Fact]
    public async Task TemplateHandlers_ShouldBeForbidden_ForSomeoneWhoCannotManageTemplates()
    {
        SignIn(TestPrincipals.UserManager());

        Assert.IsType<ForbidResult>(await _model.OnPostMakeLiveAsync(GivenTemplateId));
        Assert.IsType<ForbidResult>(await _model.OnPostMakeNotLiveAsync(GivenTemplateId));
        Assert.IsType<ForbidResult>(await _model.OnPostOpenTemplateAsync(GivenTemplateId));
        Assert.IsType<ForbidResult>(await _model.OnPostClearAllAsync());

        Assert.Empty(_adminHome.ReceivedCalls());
        Assert.Empty(_cacheInvalidator.ReceivedCalls());
        Assert.Empty(_selection.ReceivedCalls());
    }

    [Fact]
    public async Task OnPostOpenTemplate_ShouldShowAnErrorAndCallNoApi_WhenTheManagerHasNoGrantOnTheTemplate()
    {
        SignIn(TestPrincipals.TemplateManager(GivenTemplateId));

        var result = await _model.OnPostOpenTemplateAsync(OtherTemplateId);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(AdminAccessHelper.NoTemplateAccessText, _context.TempData["AdminError"]);
        Assert.Empty(_adminHome.ReceivedCalls());
        Assert.Empty(_selection.ReceivedCalls());
    }

    [Fact]
    public async Task OnPostOpenTemplate_ShouldSelectTheTemplateAndOpenTheDashboard_WhenItCanBeOpened()
    {
        var template = new TemplateDto { TemplateId = GivenTemplateId, Name = "Transfers", CreatedOn = DateTime.UtcNow };
        _adminHome.OpenTemplateAsync(Arg.Any<AdminHomeWorkState>(), GivenTemplateId, Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                call.Arg<AdminHomeWorkState>().TemplateToOpen = template;
                return AdminPageOutcome.Redirect();
            });

        var result = await _model.OnPostOpenTemplateAsync(GivenTemplateId);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Applications/Dashboard", redirect.PageName);
        await _selection.Received(1).SelectTemplateAsync(_context.HttpContext, template, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnPostOpenTemplate_ShouldStayOnThePage_WhenTheTemplateCannotBeOpened()
    {
        _adminHome.OpenTemplateAsync(Arg.Any<AdminHomeWorkState>(), GivenTemplateId, Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var state = call.Arg<AdminHomeWorkState>();
                state.HasError = true;
                state.ErrorMessage = "Could not open";
                return AdminPageOutcome.Stay();
            });

        var result = await _model.OnPostOpenTemplateAsync(GivenTemplateId);

        Assert.IsType<PageResult>(result);
        Assert.True(_model.HasError);
        Assert.Equal("Could not open", _model.ErrorMessage);
        Assert.Empty(_selection.ReceivedCalls());
    }

    [Fact]
    public async Task OnPostOpenTemplate_ShouldStayOnThePage_WhenNoTemplateWasFoundToOpen()
    {
        _adminHome.OpenTemplateAsync(Arg.Any<AdminHomeWorkState>(), GivenTemplateId, Arg.Any<CancellationToken>())
            .Returns(AdminPageOutcome.Redirect());

        var result = await _model.OnPostOpenTemplateAsync(GivenTemplateId);

        Assert.IsType<PageResult>(result);
        Assert.Empty(_selection.ReceivedCalls());
    }

    [Fact]
    public async Task OnPostClearAll_ShouldClearTheSessionAndTheSelectedTemplatesCache()
    {
        _context.Session.SetString("TemplateId", GivenTemplateId.ToString());
        _context.Session.SetString("ApplicationId", "app-1");

        var result = await _model.OnPostClearAllAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Applications/Dashboard", redirect.PageName);
        Assert.Empty(_context.Session.Keys);
        await _cacheInvalidator.Received(1).InvalidateAsync(GivenTemplateId.ToString());
    }

    [Fact]
    public async Task OnPostClearAll_ShouldStillRedirect_WhenTheCacheCannotBeCleared()
    {
        _context.Session.SetString("TemplateId", GivenTemplateId.ToString());
        _cacheInvalidator.InvalidateAsync(Arg.Any<string>()).ThrowsAsync(new InvalidOperationException("Redis down"));

        var result = await _model.OnPostClearAllAsync();

        Assert.IsType<RedirectToPageResult>(result);
        Assert.True(_model.HasError);
    }

    [Fact]
    public async Task OnPostClearAll_ShouldNotTouchTheCache_WhenNoTemplateIsSelected()
    {
        await _model.OnPostClearAllAsync();

        Assert.Empty(_cacheInvalidator.ReceivedCalls());
        Assert.True(_model.ShowSuccess);
    }

    [Fact]
    public async Task OnGet_ShouldLoadTheHomeForTheTenantAndShowItsMessages()
    {
        _context.TempData["AdminSuccess"] = "Saved";
        _context.TempData["AdminError"] = "But check this";
        _context.Session.SetString("TemplateId", GivenTemplateId.ToString());
        _context.Session.SetString("TestAuth:Token", "test-token");
        _tokenStore.GetToken().Returns("internal-token");
        GivenIdToken("id-token");
        var templates = new List<TemplateDto> { new() { TemplateId = GivenTemplateId, Name = "Transfers", CreatedOn = DateTime.UtcNow } };
        _adminHome.LoadAsync(Arg.Any<AdminHomeWorkState>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var state = call.Arg<AdminHomeWorkState>();
                state.TemplateName = "Transfers";
                state.TemplateDescription = "Academy transfers";
                state.TaskGroupCount = 3;
                state.CurrentTemplateVersion = "1.2.0";
                state.TenantTemplates = templates;
                return Task.CompletedTask;
            });

        var result = await _model.OnGetAsync();

        Assert.IsType<PageResult>(result);
        await _adminHome.Received(1).LoadAsync(
            Arg.Is<AdminHomeWorkState>(state =>
                state.TenantId == TenantId
                && state.IncludeTenantConfigurationSummary
                && state.TemplateId == GivenTemplateId.ToString()),
            Arg.Any<CancellationToken>());
        Assert.True(_model.ShowSuccess);
        Assert.Equal("Saved", _model.SuccessMessage);
        Assert.True(_model.HasError);
        Assert.Equal("But check this", _model.ErrorMessage);
        Assert.Equal("Transfers", _model.TemplateName);
        Assert.Equal("Academy transfers", _model.TemplateDescription);
        Assert.Equal(3, _model.TaskGroupCount);
        Assert.Equal("1.2.0", _model.CurrentTemplateVersion);
        Assert.Same(templates, _model.TenantTemplates);
        Assert.Equal("id-token", _model.DsiToken);
        Assert.Equal("internal-token", _model.UserToken);
        Assert.Equal("test-token", _model.TestToken);
        Assert.Equal(
            FlexFormsCacheKeys.FormTemplate(CacheKeyHelper.GenerateHashedCacheKey(GivenTemplateId.ToString())),
            _model.TemplateCacheKey);
    }

    [Fact]
    public async Task OnGet_ShouldNotAskForTheTenantSummary_ForASuperAdmin()
    {
        SignIn(TestPrincipals.SuperAdmin());
        GivenIdToken(null);

        await _model.OnGetAsync();

        await _adminHome.Received(1).LoadAsync(
            Arg.Is<AdminHomeWorkState>(state => !state.IncludeTenantConfigurationSummary && state.TemplateId == null),
            Arg.Any<CancellationToken>());
        Assert.Null(_model.TemplateCacheKey);
        Assert.False(_model.HasError);
    }

    [Fact]
    public async Task OnGet_ShouldShowTheLoadError()
    {
        GivenIdToken(null);
        _adminHome.LoadAsync(Arg.Any<AdminHomeWorkState>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var state = call.Arg<AdminHomeWorkState>();
                state.HasError = true;
                state.ErrorMessage = "Templates could not be loaded";
                return Task.CompletedTask;
            });

        await _model.OnGetAsync();

        Assert.True(_model.HasError);
        Assert.Equal("Templates could not be loaded", _model.ErrorMessage);
    }

    [Fact]
    public void Capabilities_ShouldFollowTheSignedInUser()
    {
        Assert.True(_model.IsFullAdmin);
        Assert.False(_model.IsSuperAdmin);
        Assert.True(_model.CanManageTemplates);
        Assert.True(_model.CanManageUsers);
        Assert.True(_model.CanManageRoles);
        Assert.True(_model.CanAccessTenantAdminSection);
        Assert.True(_model.CanViewTenantConfigurationSummary);
        Assert.True(_model.HasTemplateAccess(OtherTemplateId));

        SignIn(TestPrincipals.TemplateManager(GivenTemplateId));

        Assert.False(_model.IsFullAdmin);
        Assert.True(_model.CanManageTemplates);
        Assert.False(_model.CanManageUsers);
        Assert.False(_model.CanManageRoles);
        Assert.False(_model.CanManageOrganisationSettings);
        Assert.False(_model.CanManageEventMappings);
        Assert.False(_model.CanManageTenantSettings);
        Assert.False(_model.CanAccessTenantAdminSection);
        Assert.True(_model.HasTemplateAccess(GivenTemplateId));
        Assert.False(_model.HasTemplateAccess(OtherTemplateId));
    }

    [Fact]
    public void GetSessionKeysInfo_ShouldListTheCommonKeysThatAreSet()
    {
        Assert.Equal("No common session keys found", _model.GetSessionKeysInfo());

        _context.Session.SetString("TemplateId", "tpl");
        _context.Session.SetString("ApplicationReference", "APP-1");

        Assert.Equal("TemplateId: tpl, ApplicationReference: APP-1", _model.GetSessionKeysInfo());
    }

    [Fact]
    public async Task GetCacheStatusAsync_ShouldReportWhetherTheTemplateIsCached()
    {
        Assert.Equal("Cache key not available", await _model.GetCacheStatusAsync());

        _model.TemplateCacheKey = "key";
        _templateCache.ContainsAsync("key").Returns(true, false);
        Assert.Equal("Template cached", await _model.GetCacheStatusAsync());
        Assert.Equal("Template not in cache", await _model.GetCacheStatusAsync());

        _templateCache.ContainsAsync("key").ThrowsAsync(new InvalidOperationException("Redis down"));
        Assert.Equal("Unable to determine cache status", await _model.GetCacheStatusAsync());
    }

    private void SignIn(ClaimsPrincipal user)
    {
        _context = PageModelTestContext.Attach(_model, user, services => services.AddSingleton(_authentication));
        _httpContextAccessor.HttpContext.Returns(_context.HttpContext);
    }

    private void GivenIdToken(string? idToken)
    {
        var properties = new AuthenticationProperties();
        if (idToken is not null)
            properties.StoreTokens([new AuthenticationToken { Name = "id_token", Value = idToken }]);
        _authentication.AuthenticateAsync(Arg.Any<HttpContext>(), Arg.Any<string?>())
            .Returns(AuthenticateResult.Success(new AuthenticationTicket(_context.HttpContext.User, properties, "Test")));
    }
}
