using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Models;
using GovUK.Dfe.FlexForms.Application.Options;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Text;
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

    [Fact]
    public async Task LoadAsync_ShouldPopulateCatalogueFromApi_WhenCatalogueApiSucceeds()
    {
        _tenantAdmin.GetEventCatalogueAsync(Arg.Any<CancellationToken>())
            .Returns(new GetEventCatalogueResponse
            {
                Events =
                [
                    new EventCatalogueItemDto
                    {
                        EventTypeName = "CustomEvent",
                        TopicName = "custom-topic",
                        ClrTypeName = "Custom",
                        Description = "A custom event",
                        Version = "2.0",
                        Kind = EventPublishKind.Typed,
                        Properties = [new EventCataloguePropertyDto { Name = "AcademyName" }]
                    }
                ]
            });
        SetupEmptyTenantSettings();

        await _service.LoadAsync(_state);

        Assert.Equal("API", _state.CatalogueSource);
        Assert.Single(_state.Catalogue);
        Assert.Equal("CustomEvent", _state.Catalogue[0].EventTypeName);
        Assert.Equal("custom-topic", _state.Catalogue[0].TopicName);
        Assert.Contains(_state.EventTypeOptions, o => o.Value == "CustomEvent");
    }

    [Fact]
    public async Task LoadAsync_ShouldFallBackToLocalRegistry_WhenCatalogueApiFails()
    {
        _tenantAdmin.GetEventCatalogueAsync(Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("catalogue unavailable"));
        _registry.GetCatalogue().Returns([
            new EventCatalogueEntry("FallbackEvent", "fallback-topic", typeof(FallbackEventStub))
        ]);
        SetupEmptyTenantSettings();

        await _service.LoadAsync(_state);

        Assert.Equal("local registry (API unavailable)", _state.CatalogueSource);
        Assert.Single(_state.Catalogue);
        Assert.Equal("FallbackEvent", _state.Catalogue[0].EventTypeName);
        Assert.Contains("Name", _state.Catalogue[0].Properties);
    }

    [Fact]
    public async Task LoadAsync_ShouldUseEmptyCatalogue_WhenApiReturnsNoEvents()
    {
        _tenantAdmin.GetEventCatalogueAsync(Arg.Any<CancellationToken>())
            .Returns(new GetEventCatalogueResponse { Events = [] });
        SetupEmptyTenantSettings();

        await _service.LoadAsync(_state);

        Assert.Equal("API", _state.CatalogueSource);
        Assert.Empty(_state.Catalogue);
    }

    [Fact]
    public async Task LoadAsync_ShouldLoadExistingMappingAndTriggers_WhenSettingsExist()
    {
        var templateGuid = Guid.NewGuid();
        _templates.GetAccessibleTemplatesAsync(Arg.Any<CancellationToken>()).Returns([
            new TemplateDto { TemplateId = templateGuid, Name = "Transfers", CreatedOn = DateTime.UtcNow }
        ]);
        _state.SelectedTemplateId = templateGuid.ToString();
        _state.SelectedEventType = "CustomEvent";
        _state.SelectedSchemaEventType = "SchemaEvent";
        _schemaEvents.GetAll().Returns(new Dictionary<string, SchemaEventDefinitionOptions>
        {
            ["SchemaEvent"] = new() { TopicName = "schema-topic", Version = "1.0" }
        });

        var mappingsJson = $$"""
            {
              "{{templateGuid}}": {
                "CustomEvent": {
                  "mappingId": "custom-v1",
                  "eventType": "CustomEvent",
                  "fieldMappings": { "Name": { "sourceType": "DirectField", "sourceFieldId": "name" } }
                }
              }
            }
            """;
        var triggersJson = """
            {
              "FileUploaded": [
                { "eventKind": "Typed", "eventType": "CustomEvent", "mappingId": "custom-v1" }
              ]
            }
            """;
        var schemaJson = """
            {
              "SchemaEvent": {
                "topicName": "schema-topic",
                "jsonSchema": { "type": "object", "properties": {} }
              }
            }
            """;

        _tenantAdmin.GetSafeTenantSettingsAsync(_state.TenantId, Arg.Any<CancellationToken>())
            .Returns(new GetTenantSettingsResponse(
                _state.TenantId,
                "Transfers",
                [
                    TenantSetting(EventMappingsAdminService.CategoryEventMappings, mappingsJson),
                    TenantSetting(EventMappingsAdminService.CategoryEventTriggers, triggersJson),
                    TenantSetting(EventMappingsAdminService.CategorySchemaEvents, schemaJson)
                ]));

        await _service.LoadAsync(_state);

        Assert.Contains("custom-v1", _state.MappingJson);
        Assert.Single(_state.SavedTriggers);
        Assert.Equal("CustomEvent", _state.SavedTriggers[0].EventType);
        Assert.Contains("schema-topic", _state.SchemaDefinitionJson);
        Assert.Single(_state.SavedTypedMappings);
    }

    [Fact]
    public async Task LoadAsync_ShouldTolerateInvalidCategoryJson()
    {
        _tenantAdmin.GetSafeTenantSettingsAsync(_state.TenantId, Arg.Any<CancellationToken>())
            .Returns(new GetTenantSettingsResponse(
                _state.TenantId,
                "Transfers",
                [TenantSetting(EventMappingsAdminService.CategoryEventTriggers, "{ not valid json")]));

        await _service.LoadAsync(_state);

        Assert.Empty(_state.SavedTriggers);
    }

    [Fact]
    public async Task LoadAsync_ShouldClearTemplateOptions_WhenTemplatesApiFails()
    {
        _templates.GetAccessibleTemplatesAsync(Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("templates unavailable"));
        SetupEmptyTenantSettings();

        await _service.LoadAsync(_state);

        Assert.Empty(_state.TemplateOptions);
        Assert.Empty(_state.AllowedTemplateKeys);
    }

    [Fact]
    public async Task SaveTriggerAsync_ShouldStay_WhenEventTypeIsMissing()
    {
        _state.TriggerName = "FileUploaded";
        _state.TriggerEventType = " ";
        _state.TriggerMappingId = "map-1";

        var result = await _service.SaveTriggerAsync(_state);

        Assert.Contains(result.Errors, e => e.Message == EventMappingsMessages.SelectEventType);
    }

    [Fact]
    public async Task SaveTriggerAsync_ShouldStay_WhenMappingIdIsMissing()
    {
        _state.TriggerName = "FileUploaded";
        _state.TriggerEventType = "CustomEvent";
        _state.TriggerMappingId = "";

        var result = await _service.SaveTriggerAsync(_state);

        Assert.Contains(result.Errors, e => e.Message == EventMappingsMessages.EnterMappingId);
    }

    [Fact]
    public async Task SaveTriggerAsync_ShouldStay_WhenTriggerNameIsInvalid()
    {
        _state.TriggerName = "NotARealTrigger";
        _state.TriggerEventType = "CustomEvent";
        _state.TriggerMappingId = "map-1";

        var result = await _service.SaveTriggerAsync(_state);

        Assert.Contains(result.Errors, e => e.Message == EventMappingsMessages.SelectTrigger);
    }

    [Fact]
    public async Task SaveTriggerAsync_ShouldStay_WhenEventKindIsInvalid()
    {
        _state.TriggerName = "FileUploaded";
        _state.TriggerEventType = "CustomEvent";
        _state.TriggerMappingId = "map-1";
        _state.TriggerEventKind = "Invalid";

        var result = await _service.SaveTriggerAsync(_state);

        Assert.Contains(result.Errors, e => e.Message == EventMappingsMessages.EventKindMustBeTypedOrSchema);
    }

    [Fact]
    public async Task SaveTriggerAsync_ShouldReplaceExistingBinding_WhenEventTypeAlreadyBound()
    {
        _state.TriggerName = "FileUploaded";
        _state.TriggerEventType = "CustomEvent";
        _state.TriggerMappingId = "custom-v2";
        _state.TriggerEventKind = EventPublishKind.Typed;
        var existing = """
            {
              "FileUploaded": [
                { "eventKind": "Typed", "eventType": "CustomEvent", "mappingId": "custom-v1" }
              ]
            }
            """;
        SetupEmptyTenantSettings([TenantSetting(EventMappingsAdminService.CategoryEventTriggers, existing)]);

        var result = await _service.SaveTriggerAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        await _tenantAdmin.Received(1).UpsertSafeTenantSettingAsync(
            _state.TenantId,
            Arg.Is<UpsertTenantSettingRequest>(r =>
                r.Category == EventMappingsAdminService.CategoryEventTriggers
                && DecodeSettings(r.SettingsJson).Contains("custom-v2", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveTriggerAsync_ShouldStayWithError_WhenUpsertFails()
    {
        _state.TriggerName = "FileUploaded";
        _state.TriggerEventType = "CustomEvent";
        _state.TriggerMappingId = "custom-v1";
        SetupEmptyTenantSettings();
        _tenantAdmin.UpsertSafeTenantSettingAsync(
                Arg.Any<Guid>(),
                Arg.Any<UpsertTenantSettingRequest>(),
                Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("save failed"));

        var result = await _service.SaveTriggerAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.StayOnPage, result.Kind);
        Assert.True(_state.HasError);
        Assert.Contains(EventMappingsMessages.SaveTriggerFailed, result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteTriggerAsync_ShouldRedirectWithError_WhenUpsertFails()
    {
        _state.TriggerName = "FileUploaded";
        _state.TriggerEventType = "CustomEvent";
        SetupEmptyTenantSettings([TenantSetting(EventMappingsAdminService.CategoryEventTriggers, """
            {
              "FileUploaded": [
                { "eventKind": "Typed", "eventType": "CustomEvent", "mappingId": "custom-v1" }
              ]
            }
            """)]);
        _tenantAdmin.UpsertSafeTenantSettingAsync(
                Arg.Any<Guid>(),
                Arg.Any<UpsertTenantSettingRequest>(),
                Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("delete failed"));

        var result = await _service.DeleteTriggerAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Contains(EventMappingsMessages.DeleteTriggerFailed, result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteTriggerAsync_ShouldRemoveTriggerKey_WhenLastBindingRemoved()
    {
        _state.TriggerName = "FileUploaded";
        _state.TriggerEventType = "CustomEvent";
        SetupEmptyTenantSettings([TenantSetting(EventMappingsAdminService.CategoryEventTriggers, """
            {
              "FileUploaded": [
                { "eventKind": "Typed", "eventType": "CustomEvent", "mappingId": "custom-v1" }
              ]
            }
            """)]);

        var result = await _service.DeleteTriggerAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        await _tenantAdmin.Received(1).UpsertSafeTenantSettingAsync(
            _state.TenantId,
            Arg.Is<UpsertTenantSettingRequest>(r =>
                DecodeSettings(r.SettingsJson) == "{}"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveMappingAsync_ShouldStay_WhenEventTypeIsMissing()
    {
        _state.SelectedTemplateId = Guid.NewGuid().ToString();
        _state.SelectedEventType = null;
        _state.MappingJson = "{}";

        var result = await _service.SaveMappingAsync(_state);

        Assert.Contains(result.Errors, e => e.Message == EventMappingsMessages.SelectEventType);
    }

    [Fact]
    public async Task SaveMappingAsync_ShouldStay_WhenMappingJsonIsMissing()
    {
        _state.SelectedTemplateId = Guid.NewGuid().ToString();
        _state.SelectedEventType = "CustomEvent";
        _state.MappingJson = " ";

        var result = await _service.SaveMappingAsync(_state);

        Assert.Contains(result.Errors, e => e.Message == EventMappingsMessages.EnterMappingJson);
    }

    [Fact]
    public async Task SaveMappingAsync_ShouldStay_WhenMappingJsonIsInvalid()
    {
        SetupAllowedTemplate(Guid.NewGuid());
        _state.SelectedEventType = "CustomEvent";
        _state.MappingJson = "{ not-json";

        var result = await _service.SaveMappingAsync(_state);

        Assert.Contains(result.Errors, e => e.Message.StartsWith("Invalid JSON:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SaveMappingAsync_ShouldStay_WhenMappingIdIsMissing()
    {
        SetupAllowedTemplate(Guid.NewGuid());
        _state.SelectedEventType = "CustomEvent";
        _state.MappingJson = """
            {
              "mappingId": "",
              "eventType": "CustomEvent",
              "fieldMappings": { "Name": { "sourceType": "DirectField", "sourceFieldId": "name" } }
            }
            """;

        var result = await _service.SaveMappingAsync(_state);

        Assert.Contains(result.Errors, e => e.Message == EventMappingsMessages.MappingIdRequired);
    }

    [Fact]
    public async Task SaveMappingAsync_ShouldStay_WhenEventTypeDoesNotMatchSelection()
    {
        SetupAllowedTemplate(Guid.NewGuid());
        _state.SelectedEventType = "CustomEvent";
        _state.MappingJson = """
            {
              "mappingId": "custom-v1",
              "eventType": "OtherEvent",
              "fieldMappings": { "Name": { "sourceType": "DirectField", "sourceFieldId": "name" } }
            }
            """;

        var result = await _service.SaveMappingAsync(_state);

        Assert.Contains(result.Errors, e => e.Message == EventMappingsMessages.EventTypeMustMatch);
    }

    [Fact]
    public async Task SaveMappingAsync_ShouldStay_WhenFieldMappingsAreMissing()
    {
        SetupAllowedTemplate(Guid.NewGuid());
        _state.SelectedEventType = "CustomEvent";
        _state.MappingJson = """
            {
              "mappingId": "custom-v1",
              "eventType": "CustomEvent",
              "fieldMappings": {}
            }
            """;

        var result = await _service.SaveMappingAsync(_state);

        Assert.Contains(result.Errors, e => e.Message == EventMappingsMessages.FieldMappingsRequired);
    }

    [Fact]
    public async Task SaveMappingAsync_ShouldStay_WhenTemplateIsNotAllowedForTenant()
    {
        _state.SelectedTemplateId = Guid.NewGuid().ToString();
        _state.SelectedEventType = "CustomEvent";
        _state.MappingJson = """
            {
              "mappingId": "custom-v1",
              "eventType": "CustomEvent",
              "fieldMappings": { "Name": { "sourceType": "DirectField", "sourceFieldId": "name" } }
            }
            """;
        _templates.GetAccessibleTemplatesAsync(Arg.Any<CancellationToken>()).Returns([]);
        SetupEmptyTenantSettings();

        var result = await _service.SaveMappingAsync(_state);

        Assert.Contains(result.Errors, e => e.Message == EventMappingsMessages.SelectTenantTemplate);
    }

    [Fact]
    public async Task SaveMappingAsync_ShouldSetValidationWarnings_WhenUnknownPropertiesAreMapped()
    {
        var templateGuid = Guid.NewGuid();
        SetupAllowedTemplate(templateGuid);
        _tenantAdmin.GetEventCatalogueAsync(Arg.Any<CancellationToken>())
            .Returns(new GetEventCatalogueResponse
            {
                Events =
                [
                    new EventCatalogueItemDto
                    {
                        EventTypeName = "CustomEvent",
                        TopicName = "custom-topic",
                        ClrTypeName = "Custom",
                        Version = "1.0",
                        Kind = EventPublishKind.Typed,
                        Properties = [new EventCataloguePropertyDto { Name = "KnownField" }]
                    }
                ]
            });
        _state.SelectedEventType = "CustomEvent";
        _state.MappingJson = """
            {
              "mappingId": "custom-v1",
              "eventType": "CustomEvent",
              "fieldMappings": {
                "KnownField": { "sourceType": "DirectField", "sourceFieldId": "known" },
                "UnknownField": { "sourceType": "DirectField", "sourceFieldId": "unknown" }
              }
            }
            """;
        SetupEmptyTenantSettings();

        var result = await _service.SaveMappingAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Contains(_state.ValidationWarnings, w => w.Contains("UnknownField", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SaveMappingAsync_ShouldStayWithError_WhenUpsertFails()
    {
        var templateGuid = Guid.NewGuid();
        SetupAllowedTemplate(templateGuid);
        _state.SelectedEventType = "CustomEvent";
        _state.MappingJson = """
            {
              "mappingId": "custom-v1",
              "eventType": "CustomEvent",
              "fieldMappings": { "Name": { "sourceType": "DirectField", "sourceFieldId": "name" } }
            }
            """;
        SetupEmptyTenantSettings();
        _tenantAdmin.UpsertSafeTenantSettingAsync(
                Arg.Any<Guid>(),
                Arg.Any<UpsertTenantSettingRequest>(),
                Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("save failed"));

        var result = await _service.SaveMappingAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.StayOnPage, result.Kind);
        Assert.Contains(EventMappingsMessages.SaveMappingFailed, result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveSchemaAsync_ShouldStay_WhenSchemaEventTypeIsMissing()
    {
        _state.NewSchemaEventType = " ";
        _state.SchemaDefinitionJson = """{ "topicName": "t", "jsonSchema": {} }""";

        var result = await _service.SaveSchemaAsync(_state);

        Assert.Contains(result.Errors, e => e.Message == EventMappingsMessages.EnterSchemaEventType);
    }

    [Fact]
    public async Task SaveSchemaAsync_ShouldStay_WhenSchemaNameClashesWithTypedEvent()
    {
        _tenantAdmin.GetEventCatalogueAsync(Arg.Any<CancellationToken>())
            .Returns(new GetEventCatalogueResponse
            {
                Events =
                [
                    new EventCatalogueItemDto
                    {
                        EventTypeName = "CustomEvent",
                        TopicName = "custom-topic",
                        ClrTypeName = "Custom",
                        Version = "1.0",
                        Kind = EventPublishKind.Typed,
                        Properties = []
                    }
                ]
            });
        _state.NewSchemaEventType = "CustomEvent";
        _state.SchemaDefinitionJson = """{ "topicName": "t", "jsonSchema": {} }""";

        var result = await _service.SaveSchemaAsync(_state);

        Assert.Contains(result.Errors, e => e.Message == EventMappingsMessages.TypedEventNameClash("CustomEvent"));
    }

    [Fact]
    public async Task SaveSchemaAsync_ShouldStay_WhenSchemaJsonIsInvalid()
    {
        _state.NewSchemaEventType = "TenantCustomEvent";
        _state.SchemaDefinitionJson = "{ invalid";

        var result = await _service.SaveSchemaAsync(_state);

        Assert.Contains(result.Errors, e => e.Message.StartsWith("Invalid JSON:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SaveSchemaAsync_ShouldStay_WhenSchemaDefinitionIsNotAnObject()
    {
        _state.NewSchemaEventType = "TenantCustomEvent";
        _state.SchemaDefinitionJson = "[]";

        var result = await _service.SaveSchemaAsync(_state);

        Assert.Contains(result.Errors, e => e.Message == EventMappingsMessages.SchemaMustBeObject);
    }

    [Fact]
    public async Task SaveSchemaAsync_ShouldStay_WhenRequiredSchemaFieldsAreMissing()
    {
        _state.NewSchemaEventType = "TenantCustomEvent";
        _state.SchemaDefinitionJson = """{ "description": "missing required fields" }""";

        var result = await _service.SaveSchemaAsync(_state);

        Assert.Contains(result.Errors, e => e.Message == EventMappingsMessages.TopicNameRequired);
        Assert.Contains(result.Errors, e => e.Message == EventMappingsMessages.JsonSchemaRequired);
    }

    [Fact]
    public async Task SaveSchemaAsync_ShouldStayWithError_WhenUpsertFails()
    {
        _state.NewSchemaEventType = "TenantCustomEvent";
        _state.SchemaDefinitionJson = """
            {
              "topicName": "tenant-custom-topic",
              "jsonSchema": { "type": "object", "properties": {} }
            }
            """;
        SetupEmptyTenantSettings();
        _tenantAdmin.UpsertSafeTenantSettingAsync(
                Arg.Any<Guid>(),
                Arg.Any<UpsertTenantSettingRequest>(),
                Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("save failed"));

        var result = await _service.SaveSchemaAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.StayOnPage, result.Kind);
        Assert.Contains(EventMappingsMessages.SaveSchemaFailed, result.ErrorMessage, StringComparison.Ordinal);
    }

    private void SetupAllowedTemplate(Guid templateGuid)
    {
        _templates.GetAccessibleTemplatesAsync(Arg.Any<CancellationToken>()).Returns([
            new TemplateDto { TemplateId = templateGuid, Name = "Transfers", CreatedOn = DateTime.UtcNow }
        ]);
        _state.SelectedTemplateId = templateGuid.ToString();
    }

    private void SetupEmptyTenantSettings(IReadOnlyList<TenantSettingDto>? settings = null)
    {
        _tenantAdmin.GetSafeTenantSettingsAsync(_state.TenantId, Arg.Any<CancellationToken>())
            .Returns(new GetTenantSettingsResponse(_state.TenantId, "Transfers", settings ?? []));
    }

    private static string DecodeSettings(string encoded) =>
        Encoding.UTF8.GetString(Convert.FromBase64String(encoded));

    private static TenantSettingDto TenantSetting(string category, string settingsJson) =>
        new(
            Guid.NewGuid(),
            category,
            EventMappingsAdminService.TargetShared,
            settingsJson,
            false,
            DateTime.UtcNow);

    private sealed class FallbackEventStub
    {
        public string Name { get; set; } = string.Empty;
    }
}
