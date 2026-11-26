using System.Security.Claims;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Web.Pages.Templates;
using GovUK.Dfe.FlexForms.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages.Templates;

public class TemplatesIndexModelTests
{
    private readonly TemplateDto _given = Template("22222222-0000-4000-8000-000000000001", "Transfers", isLive: true);
    private readonly TemplateDto _other = Template("22222222-0000-4000-8000-000000000002", "Visits", isLive: true);
    private readonly TemplateDto _draft = Template("22222222-0000-4000-8000-000000000003", "Draft", isLive: false);

    private readonly ITemplateSelectionService _selection = Substitute.For<ITemplateSelectionService>();
    private readonly IFormTemplateProvider _formTemplates = Substitute.For<IFormTemplateProvider>();
    private readonly IndexModel _model;
    private PageModelTestContext _context = null!;

    public TemplatesIndexModelTests()
    {
        _model = new IndexModel(_selection, _formTemplates, NullLogger<IndexModel>.Instance);
        _formTemplates.GetTemplateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("No description"));
        GivenTemplates(_given, _other);
        SignIn(TestPrincipals.TemplateManager(_given.TemplateId));
    }

    [Fact]
    public async Task OnPostSelect_ShouldRefuseATemplateTheManagerHasNoGrantOn()
    {
        _model.SelectedTemplateId = _other.TemplateId;

        var result = await _model.OnPostSelectAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("You do not have access to that form.", _model.ErrorMessage);
        await _selection.DidNotReceiveWithAnyArgs().SelectTemplateAsync(default!, default!, default);
    }

    [Fact]
    public async Task OnPostSelect_ShouldRefuseATemplateTheUserCannotSee()
    {
        SignIn(TestPrincipals.User());
        _model.SelectedTemplateId = _draft.TemplateId;

        var result = await _model.OnPostSelectAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("You do not have access to that form.", _model.ErrorMessage);
        await _selection.DidNotReceiveWithAnyArgs().SelectTemplateAsync(default!, default!, default);
    }

    [Fact]
    public async Task OnPostSelect_ShouldRefuseANotLiveTemplate_WhenOnlyLiveFormsAreOffered()
    {
        SignIn(TestPrincipals.Admin());
        GivenTemplates(_given, _draft);
        _model.LiveOnly = true;
        _model.SelectedTemplateId = _draft.TemplateId;

        await _model.OnPostSelectAsync(CancellationToken.None);

        Assert.Equal("You do not have access to that form.", _model.ErrorMessage);
        Assert.Single(_model.Templates);
        await _selection.DidNotReceiveWithAnyArgs().SelectTemplateAsync(default!, default!, default);
    }

    [Fact]
    public async Task OnPostSelect_ShouldAskForAForm_WhenNoneIsChosen()
    {
        var result = await _model.OnPostSelectAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Select a form.", _model.ErrorMessage);
        await _selection.DidNotReceiveWithAnyArgs().SelectTemplateAsync(default!, default!, default);
    }

    [Theory]
    [InlineData(null, "/applications/dashboard")]
    [InlineData("/applications/APP-1", "/applications/APP-1")]
    [InlineData("/templates?liveOnly=true", "/applications/dashboard")]
    [InlineData("https://evil.example/", "/applications/dashboard")]
    [InlineData("//evil.example/", "/applications/dashboard")]
    public async Task OnPostSelect_ShouldSelectTheGivenTemplateAndReturnOnlyToALocalPage(string? returnUrl, string expected)
    {
        _model.SelectedTemplateId = _given.TemplateId;
        _model.ReturnUrl = returnUrl;

        var result = await _model.OnPostSelectAsync(CancellationToken.None);

        Assert.Equal(expected, Assert.IsType<RedirectResult>(result).Url);
        await _selection.Received(1).SelectTemplateAsync(_context.HttpContext, _given, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnPostSelect_ShouldLetSomeoneWhoReadsEveryApplicationOpenATemplateTheyWereNotGiven()
    {
        SignIn(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("permission", "Template:Any:Manage"), new Claim("permission", "Application:Any:Read")], "Test")));
        _model.SelectedTemplateId = _other.TemplateId;

        await _model.OnPostSelectAsync(CancellationToken.None);

        await _selection.Received(1).SelectTemplateAsync(_context.HttpContext, _other, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnGet_ShouldListTemplatesWithoutAGrantAsUnavailable_AndNotPreselectThem()
    {
        _selection.GetSelectedTemplateId(Arg.Any<HttpContext>()).Returns(_other.TemplateId.ToString());

        var result = await _model.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(_model.IsAdmin);
        Assert.Collection(
            _model.Templates,
            choice => Assert.True(choice.HasAccess),
            choice => Assert.False(choice.HasAccess));
        Assert.Null(_model.SelectedTemplateId);
        await _selection.DidNotReceiveWithAnyArgs().SelectTemplateAsync(default!, default!, default);
    }

    [Fact]
    public async Task OnGet_ShouldPreselectTheCurrentTemplate_WhenTheUserHasAccess()
    {
        _selection.GetSelectedTemplateId(Arg.Any<HttpContext>()).Returns(_given.TemplateId.ToString());

        await _model.OnGetAsync(CancellationToken.None);

        Assert.Equal(_given.TemplateId, _model.SelectedTemplateId);
    }

    [Fact]
    public async Task OnGet_ShouldNotOpenTheOnlyTemplate_WhenTheManagerHasNoGrantOnIt()
    {
        GivenTemplates(_other);
        _model.LiveOnly = true;

        var result = await _model.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(Assert.Single(_model.Templates).HasAccess);
        await _selection.DidNotReceiveWithAnyArgs().SelectTemplateAsync(default!, default!, default);
    }

    [Fact]
    public async Task OnGet_ShouldOpenTheOnlyTemplate_ForAUser()
    {
        SignIn(TestPrincipals.User());
        GivenTemplates(_given);

        var result = await _model.OnGetAsync(CancellationToken.None);

        Assert.Equal("/applications/dashboard", Assert.IsType<RedirectResult>(result).Url);
        Assert.False(_model.IsAdmin);
        await _selection.Received(1).SelectTemplateAsync(_context.HttpContext, _given, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnGet_ShouldLetAnAdminChoose_EvenWhenThereIsOnlyOneTemplate()
    {
        SignIn(TestPrincipals.Admin());
        GivenTemplates(_draft);

        var result = await _model.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(Assert.Single(_model.Templates).HasAccess);
    }

    [Fact]
    public async Task OnGet_ShouldShowEachTemplatesDescription_WhenItCanBeRead()
    {
        _formTemplates.GetTemplateAsync(_given.TemplateId.ToString(), Arg.Any<CancellationToken>())
            .Returns(new FormTemplate { TemplateId = "t", TemplateName = "Transfers", Description = "  Academy transfers  ", TaskGroups = [] });
        _formTemplates.GetTemplateAsync(_other.TemplateId.ToString(), Arg.Any<CancellationToken>())
            .Returns(new FormTemplate { TemplateId = "v", TemplateName = "Visits", Description = " ", TaskGroups = [] });

        await _model.OnGetAsync(CancellationToken.None);

        Assert.Equal(["Academy transfers", null], _model.Templates.Select(choice => choice.Description));
    }

    private void GivenTemplates(params TemplateDto[] templates)
        => _selection.GetSelectableTemplatesAsync(Arg.Any<CancellationToken>()).Returns(templates);

    private void SignIn(ClaimsPrincipal user) => _context = PageModelTestContext.Attach(_model, user);

    private static TemplateDto Template(string id, string name, bool isLive)
        => new() { TemplateId = Guid.Parse(id), Name = name, CreatedOn = DateTime.UtcNow, IsLive = isLive };
}
