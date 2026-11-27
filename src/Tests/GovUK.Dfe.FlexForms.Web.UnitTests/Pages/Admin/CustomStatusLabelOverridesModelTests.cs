using System.Security.Claims;
using GovUK.Dfe.CoreLibs.Caching.Helpers;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Caching;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Web.Pages.Admin;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages.Admin;

public class CustomStatusLabelOverridesModelTests
{
    private static readonly Guid GivenTemplateId = Guid.Parse("22222222-0000-4000-8000-000000000001");
    private static readonly Guid OtherTemplateId = Guid.Parse("22222222-0000-4000-8000-000000000002");

    private readonly ICustomStatusLabelOverridesAdmin _admin = Substitute.For<ICustomStatusLabelOverridesAdmin>();
    private readonly ITwoLevelCache _cache = Substitute.For<ITwoLevelCache>();
    private readonly CustomStatusLabelOverridesModel _model;
    private PageModelTestContext _context = null!;

    public CustomStatusLabelOverridesModelTests()
    {
        _model = new CustomStatusLabelOverridesModel(_admin, _cache);
        SignIn(TestPrincipals.TemplateManager(GivenTemplateId));
    }

    [Fact]
    public async Task OnPost_ShouldNotSaveOrClearTheCache_WhenThePostedTemplateHasNoGrant()
    {
        GivenValidPost(OtherTemplateId);

        var result = await _model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        AssertTemplateError();
        Assert.Null(_model.SelectedTemplateId);
        await AssertNothingSavedAsync();
        await _admin.DidNotReceiveWithAnyArgs().LoadTemplateDataAsync(default!, default, default);
    }

    [Fact]
    public async Task OnPost_ShouldNotSaveOrClearTheCache_WhenTheSessionTemplateHasNoGrant()
    {
        GivenValidPost(templateId: null);
        _context.Session.SetString("TemplateId", OtherTemplateId.ToString());

        await _model.OnPostAsync();

        AssertTemplateError();
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task OnPost_ShouldSaveAndClearTheTemplatesStatusCache_WhenTheManagerWasGivenTheTemplate()
    {
        GivenValidPost(GivenTemplateId);

        var result = await _model.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(GivenTemplateId, redirect.RouteValues!["selectedTemplateId"]);
        Assert.Equal(true, redirect.RouteValues["success"]);
        await _admin.Received(1).OverrideAsync(GivenTemplateId, ApplicationStatus.Submitted, "Sent to us", Arg.Any<CancellationToken>());
        await _cache.Received(1).RemoveAsync(
            FlexFormsCacheKeys.CustomApplicationStatuses(CacheKeyHelper.GenerateHashedCacheKey(GivenTemplateId.ToString())));
    }

    [Fact]
    public async Task OnPost_ShouldUseTheSessionTemplate_WhenNoneIsPostedAndItHasAGrant()
    {
        GivenValidPost(templateId: null);
        _context.Session.SetString("TemplateId", GivenTemplateId.ToString());

        await _model.OnPostAsync();

        await _admin.Received(1).OverrideAsync(GivenTemplateId, ApplicationStatus.Submitted, "Sent to us", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnPost_ShouldSaveForAnyTemplate_ForAnAdmin()
    {
        SignIn(TestPrincipals.Admin());
        GivenValidPost(OtherTemplateId);

        await _model.OnPostAsync();

        await _admin.Received(1).OverrideAsync(OtherTemplateId, ApplicationStatus.Submitted, "Sent to us", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnPost_ShouldAskForAValue_WhenTheOverrideIsBlank()
    {
        GivenValidPost(GivenTemplateId);
        _model.BaseStatusOverrideValue = "  ";

        var result = await _model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Contains(
            _model.ModelState[nameof(CustomStatusLabelOverridesModel.BaseStatusOverrideValue)]!.Errors,
            error => error.ErrorMessage == CustomStatusLabelOverridesMessages.OverrideRequired);
        await _admin.Received(1).LoadTemplateDataAsync(Arg.Any<CustomStatusLabelOverridesWorkState>(), GivenTemplateId, Arg.Any<CancellationToken>());
        _admin.Received(1).PopulateBaseStatuses(Arg.Any<CustomStatusLabelOverridesWorkState>());
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task OnPost_ShouldReloadForTheChosenStatus_WhenTheStatusWasChanged()
    {
        GivenValidPost(GivenTemplateId);
        _context.HttpContext.Request.QueryString = new QueryString("?status=InProgress");

        var result = await _model.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(ApplicationStatus.Submitted, redirect.RouteValues!["status"]);
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task OnGet_ShouldNotLoadATemplateTheManagerHasNoGrantOn()
    {
        var templates = new List<TemplateDto> { new() { TemplateId = OtherTemplateId, Name = "Visits", CreatedOn = DateTime.UtcNow } };
        _admin.LoadAvailableTemplatesAsync(Arg.Any<CustomStatusLabelOverridesWorkState>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                call.Arg<CustomStatusLabelOverridesWorkState>().AvailableTemplates = templates;
                return Task.CompletedTask;
            });
        _model.SelectedTemplateId = OtherTemplateId;

        var result = await _model.OnGetAsync(success: true);

        Assert.IsType<PageResult>(result);
        Assert.True(_model.ShowSuccess);
        Assert.Null(_model.SelectedTemplateId);
        Assert.Same(templates, _model.AvailableTemplates);
        await _admin.DidNotReceiveWithAnyArgs().LoadTemplateDataAsync(default!, default, default);
        await _admin.DidNotReceiveWithAnyArgs().LoadStatusOverrideAsync(default!, default, default, default);
    }

    [Fact]
    public async Task OnGet_ShouldLoadTheTemplateAndTheStatusOverride_WhenTheManagerWasGivenTheTemplate()
    {
        var template = new FormTemplate { TemplateId = "t", TemplateName = "Transfers", Description = "", TaskGroups = [] };
        IReadOnlyList<KeyValuePair<ApplicationStatus, string>> statuses = [new(ApplicationStatus.Submitted, "Submitted")];
        _admin.LoadTemplateDataAsync(Arg.Any<CustomStatusLabelOverridesWorkState>(), GivenTemplateId, Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var state = call.Arg<CustomStatusLabelOverridesWorkState>();
                state.CurrentTemplate = template;
                state.CurrentVersionNumber = "1.0.2";
                state.BaseStatuses = statuses;
                return Task.CompletedTask;
            });
        _admin.LoadStatusOverrideAsync(Arg.Any<CustomStatusLabelOverridesWorkState>(), GivenTemplateId, ApplicationStatus.Submitted, Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                call.Arg<CustomStatusLabelOverridesWorkState>().BaseStatusOverrideValue = "Sent to us";
                return Task.CompletedTask;
            });
        _context.Session.SetString("TemplateId", GivenTemplateId.ToString());

        await _model.OnGetAsync(status: ApplicationStatus.Submitted);

        Assert.Equal(GivenTemplateId, _model.SelectedTemplateId);
        Assert.Equal(ApplicationStatus.Submitted, _model.SelectedBaseStatus);
        Assert.Same(template, _model.CurrentTemplate);
        Assert.Equal("1.0.2", _model.CurrentVersionNumber);
        Assert.Same(statuses, _model.BaseStatuses);
        Assert.Equal("Sent to us", _model.BaseStatusOverrideValue);
    }

    [Fact]
    public void OnPostCancelOverride_ShouldReturnToTheAdminHome()
        => Assert.Equal("/Admin/Admin", Assert.IsType<RedirectToPageResult>(_model.OnPostCancelOverride()).PageName);

    private void GivenValidPost(Guid? templateId)
    {
        _model.SelectedTemplateId = templateId;
        _model.SelectedBaseStatus = ApplicationStatus.Submitted;
        _model.BaseStatusOverrideValue = "Sent to us";
        _context.HttpContext.Request.QueryString = new QueryString("?status=Submitted");
    }

    private void AssertTemplateError()
        => Assert.Contains(
            _model.ModelState[nameof(CustomStatusLabelOverridesModel.SelectedTemplateId)]!.Errors,
            error => error.ErrorMessage == CustomStatusLabelOverridesMessages.SelectTemplate);

    private async Task AssertNothingSavedAsync()
    {
        await _admin.DidNotReceiveWithAnyArgs().OverrideAsync(default, default, default!, default);
        await _cache.DidNotReceiveWithAnyArgs().RemoveAsync(default!);
    }

    private void SignIn(ClaimsPrincipal user) => _context = PageModelTestContext.Attach(_model, user);
}
