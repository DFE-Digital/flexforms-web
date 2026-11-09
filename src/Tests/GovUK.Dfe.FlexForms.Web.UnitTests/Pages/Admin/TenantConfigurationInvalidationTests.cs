using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Web.Pages.Admin;
using GovUK.Dfe.FlexForms.Web.Services.Tenant;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using StackExchange.Redis;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages.Admin;

public class TenantConfigurationInvalidationTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly ITenantConfigurationCache _cache = Substitute.For<ITenantConfigurationCache>();
    private readonly ITenantIdResolver _resolver = Substitute.For<ITenantIdResolver>();

    [Theory]
    [InlineData("OrganisationSettings")]
    [InlineData("TenantSettings")]
    [InlineData("EventMappings")]
    public async Task SuccessfulMutation_AwaitsSharedInvalidation_BeforeReportingSuccess(string surface)
    {
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _cache.InvalidateAsync(_tenantId, Arg.Any<CancellationToken>()).Returns(pending.Task);
        var (page, execute) = CreateOperation(surface, AdminPageOutcome.Redirect(
            successMessage: "saved", refreshLocalCaches: true));

        var result = execute();

        Assert.False(result.IsCompleted);
        Assert.Empty(page.TempData);
        _resolver.DidNotReceive().InvalidateHostnameCache();
        pending.SetResult();

        Assert.IsType<RedirectToPageResult>(await result);
        await _cache.Received(1).InvalidateAsync(_tenantId, page.HttpContext.RequestAborted);
        _resolver.Received(1).InvalidateHostnameCache();
        Assert.Contains(page.TempData, item => Equals(item.Value, "saved"));
    }

    [Theory]
    [InlineData("OrganisationSettings")]
    [InlineData("TenantSettings")]
    [InlineData("EventMappings")]
    public async Task FailedSharedInvalidation_DoesNotReportSuccess(string surface)
    {
        _cache.InvalidateAsync(_tenantId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new RedisException("Redis unavailable")));
        var (page, execute) = CreateOperation(surface, AdminPageOutcome.Redirect(
            successMessage: "saved", refreshLocalCaches: true));

        await Assert.ThrowsAsync<RedisException>(execute);

        Assert.Empty(page.TempData);
    }

    [Theory]
    [InlineData("OrganisationSettings")]
    [InlineData("TenantSettings")]
    [InlineData("EventMappings")]
    public async Task FailedMutation_DoesNotInvalidateConfiguration(string surface)
    {
        var (_, execute) = CreateOperation(surface, AdminPageOutcome.Stay(errorMessage: "save failed"));

        Assert.IsType<PageResult>(await execute());
        await _cache.DidNotReceiveWithAnyArgs().InvalidateAsync(default, default);
    }

    private (PageModel Page, Func<Task<IActionResult>> Execute) CreateOperation(
        string surface, AdminPageOutcome outcome)
    {
        var tenant = new TenantRequestContext { TenantId = _tenantId, TenantName = "Test tenant" };
        var context = new DefaultHttpContext();
        PageModel page;
        Func<Task<IActionResult>> execute;
        switch (surface)
        {
            case "OrganisationSettings":
                var organisationAdmin = Substitute.For<IOrganisationSettingsAdmin>();
                organisationAdmin.SaveAsync(Arg.Any<OrganisationSettingsWorkState>(), Arg.Any<CancellationToken>())
                    .Returns(outcome);
                var organisation = new OrganisationSettingsModel(organisationAdmin, tenant, _cache, _resolver);
                page = organisation;
                execute = () => organisation.OnPostAsync(context.RequestAborted);
                break;
            case "TenantSettings":
                var tenantAdmin = Substitute.For<ITenantSettingsAdmin>();
                tenantAdmin.RefreshAsync(Arg.Any<TenantSettingsWorkState>(), Arg.Any<CancellationToken>())
                    .Returns(outcome);
                var settings = new TenantSettingsModel(tenantAdmin, tenant, _cache, _resolver,
                    Substitute.For<IHostEnvironment>());
                page = settings;
                execute = () => settings.OnPostRefreshAsync(context.RequestAborted);
                break;
            case "EventMappings":
                var eventAdmin = Substitute.For<IEventMappingsAdmin>();
                eventAdmin.SaveMappingAsync(Arg.Any<EventMappingsWorkState>(), Arg.Any<CancellationToken>())
                    .Returns(outcome);
                var mappings = new EventMappingsModel(eventAdmin, tenant, _cache, _resolver);
                page = mappings;
                execute = () => mappings.OnPostSaveMappingAsync(context.RequestAborted);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(surface));
        }

        page.PageContext = new PageContext
        {
            HttpContext = context,
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        };
        page.TempData = new TempDataDictionary(context, Substitute.For<ITempDataProvider>());
        return (page, execute);
    }
}
