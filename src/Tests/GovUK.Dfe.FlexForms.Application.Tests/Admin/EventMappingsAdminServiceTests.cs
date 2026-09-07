using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Models;
using GovUK.Dfe.FlexForms.Application.Options;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Application.Tests.Admin;

public class EventMappingsAdminServiceTests
{
    private readonly ITenantAdminClient _tenantAdmin = Substitute.For<ITenantAdminClient>();
    private readonly ITemplatesClient _templates = Substitute.For<ITemplatesClient>();
    private readonly IEventTypeRegistry _registry = Substitute.For<IEventTypeRegistry>();
    private readonly ISchemaEventDefinitionProvider _schemaEvents = Substitute.For<ISchemaEventDefinitionProvider>();
    private readonly EventMappingsAdminService _service;
    private readonly EventMappingsWorkState _state;

    public EventMappingsAdminServiceTests()
    {
        _registry.GetCatalogue().Returns(Array.Empty<EventCatalogueEntry>());
        _schemaEvents.GetAll().Returns(new Dictionary<string, Application.Options.SchemaEventDefinitionOptions>());
        _templates.GetAccessibleTemplatesAsync(Arg.Any<CancellationToken>()).Returns([]);

        _service = new EventMappingsAdminService(
            _tenantAdmin,
            _templates,
            _registry,
            _schemaEvents,
            NullLogger<EventMappingsAdminService>.Instance);

        _state = new EventMappingsWorkState
        {
            TenantId = Guid.NewGuid(),
            TenantName = "Transfers",
            TriggerName = "FileUploaded",
            TriggerEventType = "ScanRequestedEvent",
            TriggerMappingId = "map-1"
        };
    }

    [Fact]
    public async Task SaveTriggerAsync_ShouldStay_WhenSystemOnlyEventTypeIsSelected()
    {
        var result = await _service.SaveTriggerAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.StayOnPage, result.Kind);
        Assert.Contains(result.Errors, e => e.Message.Contains("ScanRequestedEvent"));
        await _tenantAdmin.DidNotReceive().UpsertSafeTenantSettingAsync(
            Arg.Any<Guid>(),
            Arg.Any<UpsertTenantSettingRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveTriggerAsync_ShouldStay_WhenTriggerIsMissing()
    {
        _state.TriggerName = " ";
        _state.TriggerEventType = "CustomEvent";

        var result = await _service.SaveTriggerAsync(_state);

        Assert.Contains(result.Errors, e => e.Message == EventMappingsMessages.SelectTrigger);
    }

    [Fact]
    public async Task SaveMappingAsync_ShouldStay_WhenTemplateIsMissing()
    {
        _state.SelectedTemplateId = null;
        _state.SelectedEventType = "CustomEvent";
        _state.MappingJson = "{}";

        var result = await _service.SaveMappingAsync(_state);

        Assert.Contains(result.Errors, e => e.Message == EventMappingsMessages.SelectTemplate);
    }

    [Fact]
    public async Task DeleteTriggerAsync_ShouldRedirect_WhenBindingCannotBeIdentified()
    {
        _state.TriggerName = "";
        _state.TriggerEventType = "";

        var result = await _service.DeleteTriggerAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal(EventMappingsMessages.DeleteTriggerUnidentified, result.ErrorMessage);
    }

    [Fact]
    public async Task SaveMappingAsync_ShouldStay_WhenMappingIdAlreadyUsedByAnotherTemplate()
    {
        var templateGuid = Guid.Parse("9a4e9c58-9135-468c-b154-7b966f7acfb7");
        _templates.GetAccessibleTemplatesAsync(Arg.Any<CancellationToken>()).Returns([
            new TemplateDto { TemplateId = templateGuid, Name = "Transfers", CreatedOn = DateTime.UtcNow }
        ]);
        _state.AllowedTemplateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            templateGuid.ToString(),
            "form-001"
        };
        _state.SelectedTemplateId = templateGuid.ToString();
        _state.SelectedEventType = "TransferApplicationSubmittedEvent";
        _state.MappingJson = """
            {
              "mappingId": "transfer-application-submitted-v1",
              "eventType": "TransferApplicationSubmittedEvent",
              "fieldMappings": {
                "AcademyName": { "sourceType": "DirectField", "sourceFieldId": "academy" }
              }
            }
            """;

        var existingRoot = """
            {
              "form-001": {
                "TransferApplicationSubmittedEvent": {
                  "mappingId": "transfer-application-submitted-v1",
                  "eventType": "TransferApplicationSubmittedEvent",
                  "fieldMappings": { "AcademyName": { "sourceType": "DirectField", "sourceFieldId": "old" } }
                }
              }
            }
            """;

        _tenantAdmin.GetSafeTenantSettingsAsync(_state.TenantId, Arg.Any<CancellationToken>())
            .Returns(new GetTenantSettingsResponse(
                _state.TenantId,
                "Transfers",
                [
                    new TenantSettingDto(
                        Guid.NewGuid(),
                        EventMappingsAdminService.CategoryEventMappings,
                        EventMappingsAdminService.TargetShared,
                        existingRoot,
                        false,
                        DateTime.UtcNow)
                ]));

        var result = await _service.SaveMappingAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.StayOnPage, result.Kind);
        Assert.Contains(result.Errors, e => e.Message.Contains("transfer-application-submitted-v1", StringComparison.Ordinal));
        await _tenantAdmin.DidNotReceive().UpsertSafeTenantSettingAsync(
            Arg.Any<Guid>(),
            Arg.Any<UpsertTenantSettingRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveTriggerAsync_ShouldRedirect_WhenInputIsValid()
    {
        _state.TriggerName = "FileUploaded";
        _state.TriggerEventType = "CustomEvent";
        _state.TriggerMappingId = "custom-v1";
        _state.TriggerEventKind = EventPublishKind.Typed;
        _tenantAdmin.GetSafeTenantSettingsAsync(_state.TenantId, Arg.Any<CancellationToken>())
            .Returns(new GetTenantSettingsResponse(_state.TenantId, "Transfers", []));

        var result = await _service.SaveTriggerAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal(EventMappingsMessages.SavedTrigger("CustomEvent", "FileUploaded"), result.SuccessMessage);
        await _tenantAdmin.Received(1).UpsertSafeTenantSettingAsync(
            _state.TenantId,
            Arg.Is<UpsertTenantSettingRequest>(r => r.Category == EventMappingsAdminService.CategoryEventTriggers),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteTriggerAsync_ShouldRedirect_WhenBindingExists()
    {
        _state.TriggerName = "FileUploaded";
        _state.TriggerEventType = "CustomEvent";
        var existing = """
            {
              "FileUploaded": [
                { "eventKind": "Typed", "eventType": "CustomEvent", "mappingId": "custom-v1" }
              ]
            }
            """;
        _tenantAdmin.GetSafeTenantSettingsAsync(_state.TenantId, Arg.Any<CancellationToken>())
            .Returns(new GetTenantSettingsResponse(
                _state.TenantId,
                "Transfers",
                [
                    new TenantSettingDto(
                        Guid.NewGuid(),
                        EventMappingsAdminService.CategoryEventTriggers,
                        EventMappingsAdminService.TargetShared,
                        existing,
                        false,
                        DateTime.UtcNow)
                ]));

        var result = await _service.DeleteTriggerAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal(EventMappingsMessages.RemovedTrigger("CustomEvent", "FileUploaded"), result.SuccessMessage);
    }

    [Fact]
    public async Task SaveMappingAsync_ShouldRedirect_WhenMappingIsValid()
    {
        var templateGuid = Guid.NewGuid();
        _templates.GetAccessibleTemplatesAsync(Arg.Any<CancellationToken>()).Returns([
            new TemplateDto { TemplateId = templateGuid, Name = "Transfers", CreatedOn = DateTime.UtcNow }
        ]);
        _state.SelectedTemplateId = templateGuid.ToString();
        _state.SelectedEventType = "CustomEvent";
        _state.MappingJson = """
            {
              "mappingId": "custom-v1",
              "eventType": "CustomEvent",
              "fieldMappings": {
                "Name": { "sourceType": "DirectField", "sourceFieldId": "name" }
              }
            }
            """;
        _tenantAdmin.GetSafeTenantSettingsAsync(_state.TenantId, Arg.Any<CancellationToken>())
            .Returns(new GetTenantSettingsResponse(_state.TenantId, "Transfers", []));

        var result = await _service.SaveMappingAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal(
            EventMappingsMessages.SavedMapping(templateGuid.ToString(), "CustomEvent"),
            result.SuccessMessage);
    }

    [Fact]
    public async Task SaveSchemaAsync_ShouldStay_WhenSchemaDefinitionIsMissing()
    {
        _state.NewSchemaEventType = "TenantCustomEvent";
        _state.SchemaDefinitionJson = " ";

        var result = await _service.SaveSchemaAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.StayOnPage, result.Kind);
        Assert.Contains(result.Errors, e => e.Message == EventMappingsMessages.EnterSchemaDefinitionJson);
    }

    [Fact]
    public async Task SaveSchemaAsync_ShouldRedirect_WhenSchemaIsValid()
    {
        _state.NewSchemaEventType = "TenantCustomEvent";
        _state.SchemaDefinitionJson = """
            {
              "topicName": "tenant-custom-topic",
              "jsonSchema": { "type": "object", "properties": {} }
            }
            """;
        _tenantAdmin.GetSafeTenantSettingsAsync(_state.TenantId, Arg.Any<CancellationToken>())
            .Returns(new GetTenantSettingsResponse(_state.TenantId, "Transfers", []));

        var result = await _service.SaveSchemaAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal(EventMappingsMessages.SavedSchema("TenantCustomEvent"), result.SuccessMessage);
    }
}
