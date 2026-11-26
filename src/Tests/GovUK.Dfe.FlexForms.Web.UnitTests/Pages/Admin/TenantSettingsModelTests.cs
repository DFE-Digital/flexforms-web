using System.Security.Claims;
using System.Text;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Web.Pages.Admin;
using GovUK.Dfe.FlexForms.Web.Services.Tenant;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages.Admin;

public class TenantSettingsModelTests
{
    private static readonly Guid TenantId = Guid.Parse("33333333-0000-4000-8000-000000000001");

    /// <summary>The categories only a SuperAdmin may change today, named here so a change to the list is a test change.</summary>
    public static readonly TheoryData<string> RestrictedCategories = new()
    {
        "ApplicationTemplates",
        "Template",
        "ConnectionStrings",
        "FileStorage",
        "Email",
        "ApplicationInsights",
    };

    private readonly ITenantSettingsAdmin _admin = Substitute.For<ITenantSettingsAdmin>();
    private readonly ITenantConfigurationInvalidator _invalidator = Substitute.For<ITenantConfigurationInvalidator>();
    private readonly TenantRequestContext _tenant = new() { TenantId = TenantId, TenantName = "Alpha" };
    private readonly TenantSettingsModel _model;
    private PageModelTestContext _context = null!;

    public TenantSettingsModelTests()
    {
        _model = new TenantSettingsModel(_admin, _tenant, _invalidator, new HostEnvironment("Production"));
        SignIn(TestPrincipals.Admin());
        _admin.UpdateAsync(default!, default, default, default, default, default).ReturnsForAnyArgs(AdminPageOutcome.Redirect());
        _admin.DeleteAsync(default!, default, default, default).ReturnsForAnyArgs(AdminPageOutcome.Redirect());
        _admin.AddAsync(default!, default, default, default, default, default).ReturnsForAnyArgs(AdminPageOutcome.Redirect());
        _admin.ValidateAsync(default!, default, default, default, default, default).ReturnsForAnyArgs(AdminPageOutcome.Stay());
    }

    [Theory]
    [MemberData(nameof(RestrictedCategories))]
    public void CanEditCategory_ShouldBeFalseForATenantAdmin_AndTrueForASuperAdmin(string category)
    {
        Assert.False(_model.CanEditCategory(category));
        Assert.False(_model.CanEditCategory(category.ToLowerInvariant()));
        Assert.False(_model.CanEditCategory($" {category} "));

        SignIn(TestPrincipals.SuperAdmin());

        Assert.True(_model.CanEditCategory(category));
    }

    [Theory]
    [InlineData("Layout")]
    [InlineData("FeatureManagement")]
    [InlineData("")]
    [InlineData(null)]
    public void CanEditCategory_ShouldBeTrueForATenantAdmin_OutsideTheRestrictedCategories(string? category)
        => Assert.True(_model.CanEditCategory(category));

    [Theory]
    [MemberData(nameof(RestrictedCategories))]
    public async Task OnPostUpdate_ShouldRefuseATenantAdmin_ForARestrictedCategory(string category)
    {
        var result = await _model.OnPostUpdateAsync(category, "Shared", "{}", isSecret: false, CancellationToken.None);

        AssertRefused(result, $"Only SuperAdmin can update '{category}' settings.");
    }

    [Theory]
    [MemberData(nameof(RestrictedCategories))]
    public async Task OnPostDelete_ShouldRefuseATenantAdmin_ForARestrictedCategory(string category)
    {
        var result = await _model.OnPostDeleteAsync(category, "Shared", CancellationToken.None);

        AssertRefused(result, $"Only SuperAdmin can delete '{category}' settings.");
    }

    [Theory]
    [MemberData(nameof(RestrictedCategories))]
    public async Task OnPostAdd_ShouldRefuseATenantAdmin_ForARestrictedCategory(string category)
    {
        _model.NewCategory = category;

        var result = await _model.OnPostAddAsync(CancellationToken.None);

        AssertRefused(result, $"Only SuperAdmin can add '{category}' settings.");
    }

    [Theory]
    [MemberData(nameof(RestrictedCategories))]
    public async Task OnPostValidate_ShouldRefuseATenantAdmin_ForARestrictedCategory(string category)
    {
        _model.NewCategory = category;

        var result = await _model.OnPostValidateNewAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(_model.HasError);
        Assert.Equal($"Only SuperAdmin can change '{category}' settings.", _model.ErrorMessage);
        await _admin.Received(1).LoadAsync(Arg.Any<TenantSettingsWorkState>(), Arg.Any<CancellationToken>());
        await _admin.DidNotReceiveWithAnyArgs().ValidateAsync(default!, default, default, default, default, default);
    }

    [Theory]
    [MemberData(nameof(RestrictedCategories))]
    public async Task ChangeHandlers_ShouldReachTheApi_ForASuperAdmin(string category)
    {
        SignIn(TestPrincipals.SuperAdmin());
        _model.NewCategory = category;

        await _model.OnPostUpdateAsync(category, "Shared", "{}", isSecret: false, CancellationToken.None);
        await _model.OnPostDeleteAsync(category, "Shared", CancellationToken.None);
        await _model.OnPostAddAsync(CancellationToken.None);
        await _model.OnPostValidateNewAsync(CancellationToken.None);

        await _admin.Received(1).UpdateAsync(Arg.Is<TenantSettingsWorkState>(s => s.TenantId == TenantId), category, "Shared", "{}", false, Arg.Any<CancellationToken>());
        await _admin.Received(1).DeleteAsync(Arg.Is<TenantSettingsWorkState>(s => s.TenantId == TenantId), category, "Shared", Arg.Any<CancellationToken>());
        await _admin.Received(1).AddAsync(Arg.Any<TenantSettingsWorkState>(), category, "Shared", "{}", false, Arg.Any<CancellationToken>());
        await _admin.Received(1).ValidateAsync(Arg.Any<TenantSettingsWorkState>(), category, "Shared", "{}", false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnPostUpdate_ShouldReachTheApiAndRefreshTheCaches_ForATenantAdminOutsideTheRestrictedCategories()
    {
        _admin.UpdateAsync(default!, default, default, default, default, default)
            .ReturnsForAnyArgs(AdminPageOutcome.Redirect(successMessage: "Saved", refreshLocalCaches: true));

        var result = await _model.OnPostUpdateAsync("Layout", "Web", """{"ServiceName":"Alpha"}""", isSecret: false, CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        await _admin.Received(1).UpdateAsync(Arg.Any<TenantSettingsWorkState>(), "Layout", "Web", """{"ServiceName":"Alpha"}""", false, Arg.Any<CancellationToken>());
        await _invalidator.Received(1).InvalidateAsync(TenantId);
        Assert.Equal("Saved", _context.TempData["TenantSettingsSuccess"]);
    }

    [Fact]
    public async Task Handlers_ShouldNotReachTheApi_WhenThereIsNoTenant()
    {
        _tenant.TenantId = null;

        Assert.IsType<PageResult>(await _model.OnGetAsync(CancellationToken.None));
        Assert.Equal(TenantSettingsMessages.TenantContextMissing, _model.ErrorMessage);
        Assert.IsType<PageResult>(await _model.OnPostValidateAsync("Layout", "Web", "{}", false, CancellationToken.None));
        Assert.IsType<RedirectToPageResult>(await _model.OnPostUpdateAsync("Layout", "Web", "{}", false, CancellationToken.None));
        Assert.IsType<RedirectToPageResult>(await _model.OnPostImportAsync("e30=", CancellationToken.None));
        Assert.Equal(TenantSettingsMessages.TenantContextMissing, _context.TempData["TenantSettingsError"]);

        Assert.Empty(_admin.ReceivedCalls());
    }

    [Fact]
    public async Task OnGet_ShouldLoadTheTenantsSettings_AndShowTheMessagesLeftForIt()
    {
        _context.TempData["TenantSettingsSuccess"] = "Saved";
        _context.TempData["TenantSettingsError"] = "Check this";
        _admin.LoadAsync(Arg.Any<TenantSettingsWorkState>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var state = call.Arg<TenantSettingsWorkState>();
                state.HasError = true;
                state.ErrorMessage = "Audit log unavailable";
                return Task.CompletedTask;
            });

        await _model.OnGetAsync(CancellationToken.None);

        await _admin.Received(1).LoadAsync(Arg.Is<TenantSettingsWorkState>(s => s.TenantId == TenantId && s.TenantName == "Alpha"), Arg.Any<CancellationToken>());
        Assert.Equal(TenantId, _model.TenantId);
        Assert.Equal("Alpha", _model.TenantName);
        Assert.True(_model.ShowSuccess);
        Assert.Equal("Saved", _model.SuccessMessage);
        Assert.Equal("Audit log unavailable", _model.ErrorMessage);
        Assert.False(_model.CanViewPlaintextSecrets);
    }

    [Theory]
    [InlineData(null, TenantSettingsMessages.ImportFileRequired)]
    [InlineData("not base64!", TenantSettingsMessages.ImportInvalidJson)]
    public async Task OnPostImport_ShouldRefuseAMissingOrUnreadableFile(string? upload, string expectedError)
    {
        var result = await _model.OnPostImportAsync(upload, CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(expectedError, _context.TempData["TenantSettingsError"]);
        await _admin.DidNotReceiveWithAnyArgs().ImportAsync(default!, default!, default);
    }

    [Fact]
    public async Task OnPostImport_ShouldSendTheDecodedJson()
    {
        _admin.ImportAsync(default!, default!, default).ReturnsForAnyArgs(AdminPageOutcome.Stay(errorMessage: "1 setting skipped"));

        var result = await _model.OnPostImportAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"settings":[]}""")), CancellationToken.None);

        Assert.IsType<PageResult>(result);
        await _admin.Received(1).ImportAsync(Arg.Any<TenantSettingsWorkState>(), """{"settings":[]}""", Arg.Any<CancellationToken>());
        Assert.Equal("1 setting skipped", _context.TempData["TenantSettingsError"]);
    }

    [Fact]
    public async Task OnPostExport_ShouldDownloadTheFile()
    {
        _admin.ExportAsync(default!, default).ReturnsForAnyArgs(AdminPageOutcome.File([1, 2], "application/json", "alpha.json"));

        var file = Assert.IsType<FileContentResult>(await _model.OnPostExportAsync(CancellationToken.None));

        Assert.Equal("alpha.json", file.FileDownloadName);
        Assert.Equal("application/json", file.ContentType);
    }

    [Fact]
    public async Task OnPostRefresh_ShouldClearThisTenantsCachedConfiguration()
    {
        _admin.RefreshAsync(default!, default).ReturnsForAnyArgs(AdminPageOutcome.Redirect(refreshLocalCaches: true));

        await _model.OnPostRefreshAsync(CancellationToken.None);

        await _invalidator.Received(1).InvalidateAsync(TenantId);
    }

    private void AssertRefused(IActionResult result, string message)
    {
        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(message, _context.TempData["TenantSettingsError"]);
        Assert.Empty(_admin.ReceivedCalls());
        Assert.Empty(_invalidator.ReceivedCalls());
    }

    private void SignIn(ClaimsPrincipal user) => _context = PageModelTestContext.Attach(_model, user);

    private sealed class HostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "GovUK.Dfe.FlexForms.Web";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
