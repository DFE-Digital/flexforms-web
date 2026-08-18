using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Application.Tests.Admin;

public class AdminHomeServiceTests
{
    private readonly IFormTemplateProvider _templatesProvider = Substitute.For<IFormTemplateProvider>();
    private readonly ITemplatesClient _templates = Substitute.For<ITemplatesClient>();
    private readonly ITenantAdminClient _tenantAdmin = Substitute.For<ITenantAdminClient>();
    private readonly AdminHomeService _service;

    public AdminHomeServiceTests()
    {
        _service = new AdminHomeService(
            _templatesProvider,
            _templates,
            _tenantAdmin,
            NullLogger<AdminHomeService>.Instance);
    }

    [Fact]
    public async Task SetTemplateLiveAsync_ShouldRedirect_WhenApiSucceeds()
    {
        var templateId = Guid.NewGuid();

        var result = await _service.SetTemplateLiveAsync(templateId, isLive: true);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal(AdminHomeMessages.TemplateLive, result.SuccessMessage);
        await _templates.Received(1).SetTemplateLiveAsync(
            templateId,
            Arg.Is<SetTemplateLiveRequest>(r => r.IsLive),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OpenTemplateAsync_ShouldStay_WhenTemplateIsNotInCatalogue()
    {
        _templates.GetAccessibleTemplatesAsync(Arg.Any<CancellationToken>()).Returns([]);
        var state = new AdminHomeWorkState();

        var result = await _service.OpenTemplateAsync(state, Guid.NewGuid());

        Assert.Equal(AdminPageOutcomeKind.StayOnPage, result.Kind);
        Assert.Equal(AdminHomeMessages.TemplateNotInCatalogue, result.ErrorMessage);
        Assert.Null(state.TemplateToOpen);
    }

    [Fact]
    public async Task OpenTemplateAsync_ShouldRedirect_WhenTemplateExists()
    {
        var templateId = Guid.NewGuid();
        var template = new TemplateDto
        {
            TemplateId = templateId,
            Name = "Transfers",
            CreatedOn = DateTime.UtcNow,
            IsLive = true
        };
        _templates.GetAccessibleTemplatesAsync(Arg.Any<CancellationToken>()).Returns([template]);
        var state = new AdminHomeWorkState();

        var result = await _service.OpenTemplateAsync(state, templateId);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Same(template, state.TemplateToOpen);
    }

    [Fact]
    public async Task LoadAsync_ShouldLoadConfigurationSummary_WhenRequested()
    {
        var tenantId = Guid.NewGuid();
        var summary = new TenantEffectiveConfigurationDto(
            tenantId,
            "Transfers",
            "TenantConfig",
            DateTimeOffset.UtcNow,
            1,
            "Entra",
            false,
            true,
            true,
            1,
            ["localhost"],
            ["https://localhost"]);
        _templates.GetAccessibleTemplatesAsync(Arg.Any<CancellationToken>()).Returns([]);
        _tenantAdmin.GetEffectiveConfigurationAsync(tenantId, Arg.Any<CancellationToken>()).Returns(summary);
        var state = new AdminHomeWorkState
        {
            TenantId = tenantId,
            IncludeTenantConfigurationSummary = true
        };

        await _service.LoadAsync(state);

        Assert.Same(summary, state.TenantConfigurationSummary);
    }
}
