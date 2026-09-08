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
using System.Text.Json.Nodes;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Application.Tests.Admin;

public class EventMappingsAdminServiceCoverageTests
{
    private readonly ITenantAdminClient _tenantAdmin = Substitute.For<ITenantAdminClient>();
    private readonly ITemplatesClient _templates = Substitute.For<ITemplatesClient>();
    private readonly IEventTypeRegistry _registry = Substitute.For<IEventTypeRegistry>();
    private readonly ISchemaEventDefinitionProvider _schemaEvents = Substitute.For<ISchemaEventDefinitionProvider>();
    private readonly EventMappingsAdminService _service;
    private readonly EventMappingsWorkState _state;

    public EventMappingsAdminServiceCoverageTests()
    {
        _registry.GetCatalogue().Returns(Array.Empty<EventCatalogueEntry>());
        _schemaEvents.GetAll().Returns(new Dictionary<string, SchemaEventDefinitionOptions>());
        _templates.GetAccessibleTemplatesAsync(Arg.Any<CancellationToken>()).Returns([]);
        _tenantAdmin.GetEventCatalogueAsync(Arg.Any<CancellationToken>())
            .Returns(new GetEventCatalogueResponse { Events = [] });

        _service = new EventMappingsAdminService(
            _tenantAdmin,
            _templates,
            _registry,
            _schemaEvents,
            NullLogger<EventMappingsAdminService>.Instance);

        _state = new EventMappingsWorkState
        {
            TenantId = Guid.NewGuid(),
            TenantName = "Transfers"
        };
    }

    // ---------------------------------------------------------------------
    // SaveTriggerAsync
    // ---------------------------------------------------------------------

    [Fact]
    public async Task SaveTriggerAsync_ShouldAppendBinding_WhenExistingArrayHasNonObjectAndUnrelatedEntries()
    {
        _state.TriggerName = "FileUploaded";
        _state.TriggerEventType = "CustomEvent";
        _state.TriggerMappingId = "custom-v1";
        SetupSettings(TenantSetting(EventMappingsAdminService.CategoryEventTriggers, """
            {
              "FileUploaded": [
                "not-an-object",
                { "eventKind": "Typed", "eventType": "OtherEvent", "mappingId": "other-v1" }
              ]
            }
            """));

        var result = await _service.SaveTriggerAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        var bindings = (JsonArray)CapturedPayload()["FileUploaded"]!;
        Assert.Equal(3, bindings.Count);
        Assert.Equal("custom-v1", bindings[2]!["mappingId"]!.GetValue<string>());
    }

    [Fact]
    public async Task SaveTriggerAsync_ShouldReplaceBinding_WhenExistingBindingUsesPascalCasedKeys()
    {
        _state.TriggerName = "FileUploaded";
        _state.TriggerEventType = "CustomEvent";
        _state.TriggerMappingId = "custom-v2";
        SetupSettings(TenantSetting(EventMappingsAdminService.CategoryEventTriggers, """
            {
              "FileUploaded": [
                { "EventKind": "Typed", "EventType": "CustomEvent", "MappingId": "custom-v1" }
              ]
            }
            """));

        var result = await _service.SaveTriggerAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        var bindings = (JsonArray)CapturedPayload()["FileUploaded"]!;
        Assert.Single(bindings);
        Assert.Equal("custom-v2", bindings[0]!["mappingId"]!.GetValue<string>());
    }

    [Fact]
    public async Task SaveTriggerAsync_ShouldStartNewBindingArray_WhenExistingValueIsNotAnArray()
    {
        _state.TriggerName = "ApplicationSubmitted";
        _state.TriggerEventType = "CustomEvent";
        _state.TriggerMappingId = "custom-v1";
        SetupSettings(TenantSetting(
            EventMappingsAdminService.CategoryEventTriggers,
            """{ "ApplicationSubmitted": "not-an-array" }"""));

        var result = await _service.SaveTriggerAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        var bindings = (JsonArray)CapturedPayload()["ApplicationSubmitted"]!;
        Assert.Single(bindings);
    }

    [Fact]
    public async Task SaveTriggerAsync_ShouldPersistSchemaEventKind_WhenSchemaKindIsSelected()
    {
        _state.TriggerName = "ApplicationSubmitted";
        _state.TriggerEventType = "TenantCustomEvent";
        _state.TriggerMappingId = "tenant-v1";
        _state.TriggerEventKind = EventPublishKind.Schema;
        SetupSettings();

        var result = await _service.SaveTriggerAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        var bindings = (JsonArray)CapturedPayload()["ApplicationSubmitted"]!;
        Assert.Equal(EventPublishKind.Schema, bindings[0]!["eventKind"]!.GetValue<string>());
    }

    // ---------------------------------------------------------------------
    // DeleteTriggerAsync
    // ---------------------------------------------------------------------

    [Fact]
    public async Task DeleteTriggerAsync_ShouldKeepUnrelatedBindings_WhenOtherBindingsRemain()
    {
        _state.TriggerName = "FileUploaded";
        _state.TriggerEventType = "CustomEvent";
        SetupSettings(TenantSetting(EventMappingsAdminService.CategoryEventTriggers, """
            {
              "FileUploaded": [
                { "eventKind": "Typed", "eventType": "CustomEvent", "mappingId": "custom-v1" },
                { "eventKind": "Typed", "eventType": "OtherEvent", "mappingId": "other-v1" }
              ]
            }
            """));

        var result = await _service.DeleteTriggerAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        var bindings = (JsonArray)CapturedPayload()["FileUploaded"]!;
        Assert.Single(bindings);
        Assert.Equal("OtherEvent", bindings[0]!["eventType"]!.GetValue<string>());
    }

    [Fact]
    public async Task DeleteTriggerAsync_ShouldNotSave_WhenTriggerValueIsNotABindingArray()
    {
        _state.TriggerName = "FileUploaded";
        _state.TriggerEventType = "CustomEvent";
        SetupSettings(TenantSetting(
            EventMappingsAdminService.CategoryEventTriggers,
            """{ "FileUploaded": "not-an-array" }"""));

        var result = await _service.DeleteTriggerAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal(EventMappingsMessages.RemovedTrigger("CustomEvent", "FileUploaded"), result.SuccessMessage);
        await _tenantAdmin.DidNotReceive().UpsertSafeTenantSettingAsync(
            Arg.Any<Guid>(),
            Arg.Any<UpsertTenantSettingRequest>(),
            Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------------
    // SaveMappingAsync
    // ---------------------------------------------------------------------

    [Fact]
    public async Task SaveMappingAsync_ShouldStay_WhenMappingJsonIsLiteralNull()
    {
        SetupAllowedTemplate(Guid.NewGuid());
        _state.SelectedEventType = "CustomEvent";
        _state.MappingJson = "null";
        SetupSettings();

        var result = await _service.SaveMappingAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.StayOnPage, result.Kind);
        Assert.Contains(result.Errors, e => e.Message == EventMappingsMessages.MappingParseFailed);
    }

    [Fact]
    public async Task SaveMappingAsync_ShouldStay_WhenFieldMappingsIsExplicitlyNull()
    {
        SetupAllowedTemplate(Guid.NewGuid());
        _state.SelectedEventType = "CustomEvent";
        _state.MappingJson = """
            {
              "mappingId": "custom-v1",
              "eventType": "CustomEvent",
              "fieldMappings": null
            }
            """;
        SetupSettings();

        var result = await _service.SaveMappingAsync(_state);

        Assert.Contains(result.Errors, e => e.Message == EventMappingsMessages.FieldMappingsRequired);
    }

    [Fact]
    public async Task SaveMappingAsync_ShouldDefaultEventType_WhenMappingEventTypeIsBlank()
    {
        var templateGuid = Guid.NewGuid();
        SetupAllowedTemplate(templateGuid);
        _state.SelectedEventType = "CustomEvent";
        _state.MappingJson = """
            {
              "mappingId": "custom-v1",
              "eventType": "",
              "fieldMappings": { "Name": { "sourceType": "DirectField", "sourceFieldId": "name" } }
            }
            """;
        SetupSettings();

        var result = await _service.SaveMappingAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        var saved = (JsonObject)CapturedPayload()[templateGuid.ToString()]!["CustomEvent"]!;
        Assert.Equal("CustomEvent", saved["eventType"]!.GetValue<string>());
    }

    [Fact]
    public async Task SaveMappingAsync_ShouldWarnAboutEveryUnknownProperty_WhenSeveralAreMapped()
    {
        SetupAllowedTemplate(Guid.NewGuid());
        SetupCatalogue(("CustomEvent", EventPublishKind.Typed, ["KnownField"]));
        _state.SelectedEventType = "CustomEvent";
        _state.MappingJson = """
            {
              "mappingId": "custom-v1",
              "eventType": "CustomEvent",
              "fieldMappings": {
                "ZedField": { "sourceType": "DirectField", "sourceFieldId": "zed" },
                "KnownField": { "sourceType": "DirectField", "sourceFieldId": "known" },
                "AlphaField": { "sourceType": "DirectField", "sourceFieldId": "alpha" }
              }
            }
            """;
        SetupSettings();

        var result = await _service.SaveMappingAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal(
            [
                EventMappingsMessages.UnknownProperty("AlphaField", "CustomEvent"),
                EventMappingsMessages.UnknownProperty("ZedField", "CustomEvent")
            ],
            _state.ValidationWarnings);
    }

    [Fact]
    public async Task SaveMappingAsync_ShouldNotWarn_WhenCatalogueEventIsSchemaKind()
    {
        SetupAllowedTemplate(Guid.NewGuid());
        SetupCatalogue(("CustomEvent", EventPublishKind.Schema, ["KnownField"]));
        _state.SelectedEventType = "CustomEvent";
        _state.MappingJson = """
            {
              "mappingId": "custom-v1",
              "eventType": "CustomEvent",
              "fieldMappings": { "Anything": { "sourceType": "DirectField", "sourceFieldId": "x" } }
            }
            """;
        SetupSettings();

        var result = await _service.SaveMappingAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Empty(_state.ValidationWarnings);
    }

    [Fact]
    public async Task SaveMappingAsync_ShouldRemoveLegacyAliasKey_WhenAliasOnlyHeldTheSavedEvent()
    {
        var templateGuid = Guid.NewGuid();
        SetupAllowedTemplate(templateGuid);
        SetupSchemaAlias(templateGuid, """{ "templateId": "form-001" }""");
        _state.SelectedEventType = "CustomEvent";
        _state.MappingJson = ValidMappingJson;
        SetupSettings(TenantSetting(EventMappingsAdminService.CategoryEventMappings, """
            {
              "form-001": {
                "CustomEvent": { "mappingId": "custom-v1", "eventType": "CustomEvent" }
              }
            }
            """));

        var result = await _service.SaveMappingAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        var payload = CapturedPayload();
        Assert.False(payload.ContainsKey("form-001"));
        Assert.NotNull(payload[templateGuid.ToString()]!["CustomEvent"]);
    }

    [Fact]
    public async Task SaveMappingAsync_ShouldKeepLegacyAliasKey_WhenAliasStillHoldsOtherEvents()
    {
        var templateGuid = Guid.NewGuid();
        SetupAllowedTemplate(templateGuid);
        SetupSchemaAlias(templateGuid, """{ "templateId": "form-001" }""");
        _state.SelectedEventType = "CustomEvent";
        _state.MappingJson = ValidMappingJson;
        SetupSettings(TenantSetting(EventMappingsAdminService.CategoryEventMappings, """
            {
              "form-001": {
                "CustomEvent": { "mappingId": "custom-v1", "eventType": "CustomEvent" },
                "OtherEvent": { "mappingId": "other-v1", "eventType": "OtherEvent" }
              }
            }
            """));

        var result = await _service.SaveMappingAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        var alias = (JsonObject)CapturedPayload()["form-001"]!;
        Assert.False(alias.ContainsKey("CustomEvent"));
        Assert.True(alias.ContainsKey("OtherEvent"));
    }

    [Fact]
    public async Task SaveMappingAsync_ShouldResolveAlias_WhenSchemaIsBase64Encoded()
    {
        var templateGuid = Guid.NewGuid();
        SetupAllowedTemplate(templateGuid);
        SetupSchemaAlias(
            templateGuid,
            Convert.ToBase64String(Encoding.UTF8.GetBytes("""{ "templateId": "form-002" }""")));
        _state.SelectedEventType = "CustomEvent";
        _state.MappingJson = ValidMappingJson;
        SetupSettings(TenantSetting(EventMappingsAdminService.CategoryEventMappings, """
            {
              "form-002": {
                "CustomEvent": { "mappingId": "custom-v1", "eventType": "CustomEvent" }
              }
            }
            """));

        var result = await _service.SaveMappingAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.False(CapturedPayload().ContainsKey("form-002"));
    }

    [Fact]
    public async Task SaveMappingAsync_ShouldIgnoreAlias_WhenSchemaIsNeitherJsonNorBase64()
    {
        var templateGuid = Guid.NewGuid();
        SetupAllowedTemplate(templateGuid);
        SetupSchemaAlias(templateGuid, "@@@ not base64 @@@");
        _state.SelectedEventType = "CustomEvent";
        _state.MappingJson = ValidMappingJson;
        SetupSettings();

        var result = await _service.SaveMappingAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal([templateGuid.ToString()], CapturedPayload().Select(p => p.Key));
    }

    [Fact]
    public async Task SaveMappingAsync_ShouldIgnoreAlias_WhenEmbeddedTemplateIdIsNotAString()
    {
        var templateGuid = Guid.NewGuid();
        SetupAllowedTemplate(templateGuid);
        SetupSchemaAlias(templateGuid, """{ "templateId": 42 }""");
        _state.SelectedEventType = "CustomEvent";
        _state.MappingJson = ValidMappingJson;
        SetupSettings();

        var result = await _service.SaveMappingAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal([templateGuid.ToString()], CapturedPayload().Select(p => p.Key));
    }

    [Fact]
    public async Task SaveMappingAsync_ShouldIgnoreAlias_WhenSchemaHasNoEmbeddedTemplateId()
    {
        var templateGuid = Guid.NewGuid();
        SetupAllowedTemplate(templateGuid);
        SetupSchemaAlias(templateGuid, """{ "type": "object" }""");
        _state.SelectedEventType = "CustomEvent";
        _state.MappingJson = ValidMappingJson;
        SetupSettings();

        var result = await _service.SaveMappingAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal([templateGuid.ToString()], CapturedPayload().Select(p => p.Key));
    }

    [Fact]
    public async Task SaveMappingAsync_ShouldNotDuplicateAlias_WhenEmbeddedTemplateIdMatchesTheTemplateGuid()
    {
        var templateGuid = Guid.NewGuid();
        SetupAllowedTemplate(templateGuid);
        SetupSchemaAlias(templateGuid, $$"""{ "templateId": "{{templateGuid}}" }""");
        _state.SelectedEventType = "CustomEvent";
        _state.MappingJson = ValidMappingJson;
        SetupSettings();

        var result = await _service.SaveMappingAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal([templateGuid.ToString()], CapturedPayload().Select(p => p.Key));
    }

    [Fact]
    public async Task SaveMappingAsync_ShouldIgnoreAlias_WhenSchemaLookupThrows()
    {
        var templateGuid = Guid.NewGuid();
        SetupAllowedTemplate(templateGuid);
        _templates.GetLatestTemplateSchemaAsync(templateGuid, Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("schema unavailable"));
        _state.SelectedEventType = "CustomEvent";
        _state.MappingJson = ValidMappingJson;
        SetupSettings();

        var result = await _service.SaveMappingAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal([templateGuid.ToString()], CapturedPayload().Select(p => p.Key));
    }

    [Fact]
    public async Task SaveMappingAsync_ShouldSaveUnderNonGuidKey_WhenAliasKeyIsSelected()
    {
        var templateGuid = Guid.NewGuid();
        _templates.GetAccessibleTemplatesAsync(Arg.Any<CancellationToken>()).Returns([
            new TemplateDto { TemplateId = templateGuid, Name = "Transfers", CreatedOn = DateTime.UtcNow }
        ]);
        SetupSchemaAlias(templateGuid, """{ "templateId": "form-001" }""");
        _state.SelectedTemplateId = "form-001";
        _state.SelectedEventType = "CustomEvent";
        _state.MappingJson = ValidMappingJson;
        SetupSettings();

        var result = await _service.SaveMappingAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal(["form-001"], CapturedPayload().Select(p => p.Key));
    }

    [Fact]
    public async Task SaveMappingAsync_ShouldSave_WhenExistingRootHasUnrelatedAndMalformedEntries()
    {
        var templateGuid = Guid.NewGuid();
        SetupAllowedTemplate(templateGuid);
        _state.SelectedEventType = "CustomEvent";
        _state.MappingJson = ValidMappingJson;
        SetupSettings(TenantSetting(EventMappingsAdminService.CategoryEventMappings, """
            {
              "BasePath": "/config",
              "bad-template": "not-an-object",
              "form-001": {
                "BadEvent": "not-an-object",
                "OtherEvent": { "mappingId": "other-v1", "eventType": "OtherEvent" },
                "NoIdEvent": { "eventType": "NoIdEvent" }
              }
            }
            """));

        var result = await _service.SaveMappingAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.NotNull(CapturedPayload()[templateGuid.ToString()]!["CustomEvent"]);
    }

    [Fact]
    public async Task SaveMappingAsync_ShouldStay_WhenSameMappingIdIsUsedForADifferentEventOnTheSameTemplate()
    {
        var templateGuid = Guid.NewGuid();
        SetupAllowedTemplate(templateGuid);
        _state.SelectedEventType = "CustomEvent";
        _state.MappingJson = ValidMappingJson;
        SetupSettings(TenantSetting(EventMappingsAdminService.CategoryEventMappings, $$"""
            {
              "{{templateGuid}}": {
                "OtherEvent": { "mappingId": "custom-v1", "eventType": "OtherEvent" }
              }
            }
            """));

        var result = await _service.SaveMappingAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.StayOnPage, result.Kind);
        Assert.Contains(result.Errors, e => e.Message == EventMappingsMessages.DuplicateMappingId(
            "custom-v1",
            $"template '{templateGuid}' / OtherEvent"));
    }

    // ---------------------------------------------------------------------
    // LoadAsync - saved typed mappings
    // ---------------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_ShouldSkipBasePathAndMalformedNodes_WhenReadingSavedTypedMappings()
    {
        var templateGuid = Guid.NewGuid();
        _schemaEvents.GetAll().Returns(new Dictionary<string, SchemaEventDefinitionOptions>
        {
            ["SchemaEvent"] = new() { TopicName = "schema-topic" }
        });
        SetupSettings(TenantSetting(EventMappingsAdminService.CategoryEventMappings, $$"""
            {
              "BasePath": "/config",
              "bad-template": "not-an-object",
              "{{templateGuid}}": {
                "CustomEvent": { "mappingId": "custom-v1" },
                "BadEvent": "not-an-object",
                "SchemaEvent": { "mappingId": "schema-v1" }
              }
            }
            """));

        await _service.LoadAsync(_state);

        Assert.Single(_state.SavedTypedMappings);
        Assert.Equal("CustomEvent", _state.SavedTypedMappings[0].EventType);
    }

    [Fact]
    public async Task LoadAsync_ShouldPreferGuidTemplateKeyAndOrderByEventType_WhenMappingsAreDuplicated()
    {
        var templateGuid = Guid.NewGuid();
        SetupSettings(TenantSetting(EventMappingsAdminService.CategoryEventMappings, $$"""
            {
              "form-001": {
                "ZedEvent": { "mappingId": "zed-v1" }
              },
              "{{templateGuid}}": {
                "ZedEvent": { "mappingId": "zed-v1" },
                "AlphaEvent": { "mappingId": "alpha-v1", "description": "alpha mapping" }
              }
            }
            """));

        await _service.LoadAsync(_state);

        Assert.Equal(
            ["AlphaEvent", "ZedEvent"],
            _state.SavedTypedMappings.Select(r => r.EventType));
        Assert.Equal(templateGuid.ToString(), _state.SavedTypedMappings[1].TemplateId);
    }

    [Fact]
    public async Task LoadAsync_ShouldReadPascalCasedMappingFields_AndFallBackWhenMappingIdIsAbsent()
    {
        var templateGuid = Guid.NewGuid();
        SetupSettings(TenantSetting(EventMappingsAdminService.CategoryEventMappings, $$"""
            {
              "{{templateGuid}}": {
                "PascalEvent": { "MappingId": "pascal-v1", "Description": "pascal description" },
                "NoIdEvent": { "eventType": "NoIdEvent" }
              }
            }
            """));

        await _service.LoadAsync(_state);

        var pascal = _state.SavedTypedMappings.Single(r => r.EventType == "PascalEvent");
        Assert.Equal("pascal-v1", pascal.MappingId);
        Assert.Equal("pascal description", pascal.Description);

        var noId = _state.SavedTypedMappings.Single(r => r.EventType == "NoIdEvent");
        Assert.Equal("—", noId.MappingId);
        Assert.Null(noId.Description);
    }

    [Fact]
    public async Task LoadAsync_ShouldFilterSavedMappings_WhenTemplateKeyIsNotAllowed()
    {
        var templateGuid = Guid.NewGuid();
        _templates.GetAccessibleTemplatesAsync(Arg.Any<CancellationToken>()).Returns([
            new TemplateDto { TemplateId = templateGuid, Name = "Transfers", CreatedOn = DateTime.UtcNow }
        ]);
        SetupSettings(TenantSetting(EventMappingsAdminService.CategoryEventMappings, $$"""
            {
              "{{templateGuid}}": { "CustomEvent": { "mappingId": "custom-v1" } },
              "someone-elses-template": { "OtherEvent": { "mappingId": "other-v1" } }
            }
            """));

        await _service.LoadAsync(_state);

        Assert.Single(_state.SavedTypedMappings);
        Assert.Equal(templateGuid.ToString(), _state.SavedTypedMappings[0].TemplateId);
    }

    // ---------------------------------------------------------------------
    // LoadAsync - saved triggers
    // ---------------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_ShouldSkipMalformedTriggerNodes_AndApplyBindingDefaults()
    {
        SetupSettings(TenantSetting(EventMappingsAdminService.CategoryEventTriggers, """
            {
              "FileUploaded": "not-an-array",
              "ApplicationSubmitted": [
                "not-an-object",
                { "mappingId": "no-event-type" },
                { "eventType": "CustomEvent" }
              ]
            }
            """));

        await _service.LoadAsync(_state);

        var binding = Assert.Single(_state.SavedTriggers);
        Assert.Equal("ApplicationSubmitted", binding.Trigger);
        Assert.Equal("CustomEvent", binding.EventType);
        Assert.Equal(EventPublishKind.Typed, binding.EventKind);
        Assert.Equal("—", binding.MappingId);
    }

    [Fact]
    public async Task LoadAsync_ShouldOrderSavedTriggers_WhenSeveralBindingsExist()
    {
        SetupSettings(TenantSetting(EventMappingsAdminService.CategoryEventTriggers, """
            {
              "FileUploaded": [
                { "eventKind": "Typed", "eventType": "ZedEvent", "mappingId": "zed-v1" },
                { "eventKind": "Typed", "eventType": "AlphaEvent", "mappingId": "alpha-v1" }
              ],
              "ApplicationSubmitted": [
                { "EventKind": "Schema", "EventType": "SchemaEvent", "MappingId": "schema-v1" }
              ]
            }
            """));

        await _service.LoadAsync(_state);

        Assert.Equal(
            [
                ("ApplicationSubmitted", "SchemaEvent"),
                ("FileUploaded", "AlphaEvent"),
                ("FileUploaded", "ZedEvent")
            ],
            _state.SavedTriggers.Select(r => (r.Trigger, r.EventType)));
        Assert.Equal(EventPublishKind.Schema, _state.SavedTriggers[0].EventKind);
    }

    // ---------------------------------------------------------------------
    // LoadAsync - schema definition
    // ---------------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_ShouldUseProviderDefinition_WhenTenantSettingsHaveNoMatchingSchema()
    {
        _state.SelectedSchemaEventType = "TenantCustomEvent";
        _schemaEvents.GetDefinition("TenantCustomEvent").Returns(new SchemaEventDefinitionOptions
        {
            TopicName = "provider-topic",
            Version = "3.0",
            Description = "from provider",
            JsonSchema = new Dictionary<string, object?> { ["type"] = "string" }
        });
        SetupSettings(TenantSetting(
            EventMappingsAdminService.CategorySchemaEvents,
            """{ "AnotherSchemaEvent": { "topicName": "other-topic" } }"""));

        await _service.LoadAsync(_state);

        Assert.Contains("provider-topic", _state.SchemaDefinitionJson);
        Assert.Contains("from provider", _state.SchemaDefinitionJson);
    }

    [Fact]
    public async Task LoadAsync_ShouldUseDefaultJsonSchema_WhenProviderDefinitionHasNoSchema()
    {
        _state.SelectedSchemaEventType = "TenantCustomEvent";
        _schemaEvents.GetDefinition("TenantCustomEvent").Returns(new SchemaEventDefinitionOptions
        {
            TopicName = "provider-topic",
            JsonSchema = null
        });
        SetupSettings();

        await _service.LoadAsync(_state);

        Assert.Contains("provider-topic", _state.SchemaDefinitionJson);
        Assert.Contains("\"type\": \"object\"", _state.SchemaDefinitionJson);
    }

    [Fact]
    public async Task LoadAsync_ShouldUseEmptySchemaTemplate_WhenProviderHasNoDefinition()
    {
        _state.SelectedSchemaEventType = "TenantCustomEvent";
        SetupSettings();

        await _service.LoadAsync(_state);

        Assert.Contains("my-custom-topic", _state.SchemaDefinitionJson);
        Assert.Equal("TenantCustomEvent", _state.NewSchemaEventType);
    }

    [Fact]
    public async Task LoadAsync_ShouldNormaliseSchemaKeyCasing_WhenSettingsUseDifferentCasing()
    {
        _state.SelectedSchemaEventType = "tenantcustomevent";
        SetupSettings(TenantSetting(
            EventMappingsAdminService.CategorySchemaEvents,
            """{ "TenantCustomEvent": { "topicName": "stored-topic" } }"""));

        await _service.LoadAsync(_state);

        Assert.Equal("TenantCustomEvent", _state.NewSchemaEventType);
        Assert.Equal("TenantCustomEvent", _state.SelectedSchemaEventType);
        Assert.Contains("stored-topic", _state.SchemaDefinitionJson);
    }

    [Fact]
    public async Task LoadAsync_ShouldKeepExistingSchemaJson_WhenNoSchemaEventIsSelected()
    {
        _state.SchemaDefinitionJson = """{ "topicName": "already-entered" }""";
        SetupSettings();

        await _service.LoadAsync(_state);

        Assert.Equal("""{ "topicName": "already-entered" }""", _state.SchemaDefinitionJson);
    }

    // ---------------------------------------------------------------------
    // LoadAsync - selected mapping
    // ---------------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_ShouldFindMappingByScanningAllTemplates_WhenLookupKeysDoNotMatch()
    {
        _state.SelectedTemplateId = Guid.NewGuid().ToString();
        _state.SelectedEventType = "CustomEvent";
        SetupSettings(TenantSetting(EventMappingsAdminService.CategoryEventMappings, """
            {
              "legacy-template": {
                "CustomEvent": { "mappingId": "legacy-v1", "eventType": "CustomEvent" }
              }
            }
            """));

        await _service.LoadAsync(_state);

        Assert.Contains("legacy-v1", _state.MappingJson);
    }

    [Fact]
    public async Task LoadAsync_ShouldUseEmptyMappingTemplate_WhenNoTemplateHoldsTheSelectedEvent()
    {
        _state.SelectedTemplateId = Guid.NewGuid().ToString();
        _state.SelectedEventType = "CustomEvent";
        SetupSettings(TenantSetting(EventMappingsAdminService.CategoryEventMappings, """
            {
              "legacy-template": {
                "OtherEvent": { "mappingId": "other-v1", "eventType": "OtherEvent" }
              }
            }
            """));

        await _service.LoadAsync(_state);

        Assert.Contains("custom-event-v1", _state.MappingJson);
    }

    [Fact]
    public async Task LoadAsync_ShouldUseGenericMappingId_WhenNoEventTypeIsSelected()
    {
        SetupSettings();

        await _service.LoadAsync(_state);

        Assert.Contains("mapping-v1", _state.MappingJson);
    }

    [Fact]
    public async Task LoadAsync_ShouldFallBackEverywhere_WhenTenantSettingsApiThrows()
    {
        _state.SelectedTemplateId = Guid.NewGuid().ToString();
        _state.SelectedEventType = "CustomEvent";
        _state.SelectedSchemaEventType = "TenantCustomEvent";
        _tenantAdmin.GetSafeTenantSettingsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("settings unavailable"));

        await _service.LoadAsync(_state);

        Assert.Empty(_state.SavedTypedMappings);
        Assert.Empty(_state.SavedTriggers);
        Assert.Contains("custom-event-v1", _state.MappingJson);
        Assert.Contains("my-custom-topic", _state.SchemaDefinitionJson);
    }

    // ---------------------------------------------------------------------
    // LoadAsync - tenant setting selection and page data
    // ---------------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_ShouldUseWebTargetSetting_WhenNoSharedTargetExists()
    {
        SetupSettings(new TenantSettingDto(
            Guid.NewGuid(),
            EventMappingsAdminService.CategoryEventTriggers,
            EventMappingsAdminService.TargetWeb,
            """{ "FileUploaded": [ { "eventType": "WebEvent", "mappingId": "web-v1" } ] }""",
            false,
            DateTime.UtcNow));

        await _service.LoadAsync(_state);

        Assert.Equal("WebEvent", Assert.Single(_state.SavedTriggers).EventType);
    }

    [Fact]
    public async Task LoadAsync_ShouldUseFirstSetting_WhenNeitherSharedNorWebTargetExists()
    {
        SetupSettings(new TenantSettingDto(
            Guid.NewGuid(),
            EventMappingsAdminService.CategoryEventTriggers,
            "Api",
            """{ "FileUploaded": [ { "eventType": "ApiEvent", "mappingId": "api-v1" } ] }""",
            false,
            DateTime.UtcNow));

        await _service.LoadAsync(_state);

        Assert.Equal("ApiEvent", Assert.Single(_state.SavedTriggers).EventType);
    }

    [Fact]
    public async Task LoadAsync_ShouldTreatNonObjectSettingsJsonAsEmpty()
    {
        SetupSettings(TenantSetting(EventMappingsAdminService.CategoryEventTriggers, "[]"));

        await _service.LoadAsync(_state);

        Assert.Empty(_state.SavedTriggers);
    }

    [Fact]
    public async Task LoadAsync_ShouldNotDuplicateEventOption_WhenSchemaEventSharesCatalogueName()
    {
        SetupCatalogue(("SharedEvent", EventPublishKind.Typed, []));
        _schemaEvents.GetAll().Returns(new Dictionary<string, SchemaEventDefinitionOptions>
        {
            ["SharedEvent"] = new() { TopicName = "shared-topic" }
        });
        SetupSettings();

        await _service.LoadAsync(_state);

        Assert.Single(_state.EventTypeOptions, o => o.Value == "SharedEvent");
    }

    [Fact]
    public async Task LoadAsync_ShouldOrderEventTypeOptionsByText_WhenSeveralEventsExist()
    {
        SetupCatalogue(
            ("ZedEvent", EventPublishKind.Typed, []),
            ("AlphaEvent", EventPublishKind.Typed, []));
        SetupSettings();

        await _service.LoadAsync(_state);

        Assert.Equal(
            ["AlphaEvent", "ZedEvent"],
            _state.EventTypeOptions.Select(o => o.Value));
    }

    [Fact]
    public async Task LoadAsync_ShouldExcludeSystemOnlyEventFromTriggerOptions()
    {
        SetupCatalogue(
            (EventMappingsAdminService.SystemOnlyEventType, EventPublishKind.Typed, []),
            ("CustomEvent", EventPublishKind.Typed, []));
        SetupSettings();

        await _service.LoadAsync(_state);

        Assert.Contains(_state.EventTypeOptions, o => o.Value == EventMappingsAdminService.SystemOnlyEventType);
        Assert.DoesNotContain(
            _state.TriggerEventTypeOptions,
            o => o.Value == EventMappingsAdminService.SystemOnlyEventType);
    }

    [Fact]
    public async Task LoadAsync_ShouldPopulateClrPropertyHints_WhenSelectedEventIsInCatalogue()
    {
        SetupCatalogue(("CustomEvent", EventPublishKind.Typed, ["AcademyName", "Urn"]));
        _state.SelectedEventType = "CustomEvent";
        SetupSettings();

        await _service.LoadAsync(_state);

        Assert.Equal(["AcademyName", "Urn"], _state.ClrPropertyHints);
    }

    [Fact]
    public async Task LoadAsync_ShouldClearClrPropertyHints_WhenSelectedEventIsNotInCatalogue()
    {
        _state.SelectedEventType = "UnknownEvent";
        _state.ClrPropertyHints = ["stale"];
        SetupSettings();

        await _service.LoadAsync(_state);

        Assert.Empty(_state.ClrPropertyHints);
    }

    [Fact]
    public async Task LoadAsync_ShouldDefaultCatalogueKindAndTopic_WhenApiOmitsThem()
    {
        _tenantAdmin.GetEventCatalogueAsync(Arg.Any<CancellationToken>())
            .Returns(new GetEventCatalogueResponse
            {
                Events =
                [
                    new EventCatalogueItemDto
                    {
                        EventTypeName = "CustomEvent",
                        TopicName = null,
                        ClrTypeName = "Custom",
                        Version = "1.0",
                        Kind = " ",
                        Properties = null!
                    }
                ]
            });
        SetupSettings();

        await _service.LoadAsync(_state);

        var row = Assert.Single(_state.Catalogue);
        Assert.Equal("(no topic resolved)", row.TopicName);
        Assert.Equal(EventPublishKind.Typed, row.Kind);
        Assert.Empty(row.Properties);
    }

    [Fact]
    public async Task LoadAsync_ShouldSkipEmptyTemplateIds_AndLabelUnnamedTemplatesById()
    {
        var namedTemplate = Guid.NewGuid();
        var unnamedTemplate = Guid.NewGuid();
        _templates.GetAccessibleTemplatesAsync(Arg.Any<CancellationToken>()).Returns([
            new TemplateDto { TemplateId = Guid.Empty, Name = "Ignored", CreatedOn = DateTime.UtcNow },
            new TemplateDto { TemplateId = namedTemplate, Name = "Transfers", CreatedOn = DateTime.UtcNow },
            new TemplateDto { TemplateId = unnamedTemplate, Name = " ", CreatedOn = DateTime.UtcNow }
        ]);
        SetupSettings();

        await _service.LoadAsync(_state);

        Assert.Equal(2, _state.TemplateOptions.Count);
        Assert.Contains(_state.TemplateOptions, o => o.Text == $"Transfers ({namedTemplate})");
        Assert.Contains(_state.TemplateOptions, o => o.Text == unnamedTemplate.ToString());
    }

    // ---------------------------------------------------------------------
    // SaveSchemaAsync
    // ---------------------------------------------------------------------

    [Fact]
    public async Task SaveSchemaAsync_ShouldFallBackToSelectedSchemaEventType_WhenNewNameIsBlank()
    {
        _state.NewSchemaEventType = null;
        _state.SelectedSchemaEventType = "TenantCustomEvent";
        _state.SchemaDefinitionJson = """{ "topicName": "t", "jsonSchema": { "type": "object" } }""";
        SetupSettings();

        var result = await _service.SaveSchemaAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal("TenantCustomEvent", _state.SelectedEventType);
        Assert.NotNull(CapturedPayload()["TenantCustomEvent"]);
    }

    [Fact]
    public async Task SaveSchemaAsync_ShouldAcceptPascalCasedRequiredFields()
    {
        _state.NewSchemaEventType = "TenantCustomEvent";
        _state.SchemaDefinitionJson = """{ "TopicName": "t", "JsonSchema": { "type": "object" } }""";
        SetupSettings();

        var result = await _service.SaveSchemaAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
    }

    [Fact]
    public async Task SaveSchemaAsync_ShouldStay_WhenOnlyTopicNameIsMissing()
    {
        _state.NewSchemaEventType = "TenantCustomEvent";
        _state.SchemaDefinitionJson = """{ "jsonSchema": { "type": "object" } }""";

        var result = await _service.SaveSchemaAsync(_state);

        Assert.Contains(result.Errors, e => e.Message == EventMappingsMessages.TopicNameRequired);
        Assert.DoesNotContain(result.Errors, e => e.Message == EventMappingsMessages.JsonSchemaRequired);
    }

    [Fact]
    public async Task SaveSchemaAsync_ShouldReplaceExistingDefinition_WhenSchemaKeyAlreadyExists()
    {
        _state.NewSchemaEventType = "TenantCustomEvent";
        _state.SchemaDefinitionJson = """{ "topicName": "new-topic", "jsonSchema": { "type": "object" } }""";
        SetupSettings(TenantSetting(
            EventMappingsAdminService.CategorySchemaEvents,
            """{ "TenantCustomEvent": { "topicName": "old-topic", "jsonSchema": {} } }"""));

        var result = await _service.SaveSchemaAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        var stored = (JsonObject)CapturedPayload()["TenantCustomEvent"]!;
        Assert.Equal("new-topic", stored["topicName"]!.GetValue<string>());
    }

    [Fact]
    public async Task SaveSchemaAsync_ShouldRefreshTenantConfiguration_WhenSaveSucceeds()
    {
        _state.NewSchemaEventType = "TenantCustomEvent";
        _state.SchemaDefinitionJson = """{ "topicName": "t", "jsonSchema": { "type": "object" } }""";
        SetupSettings();

        await _service.SaveSchemaAsync(_state);

        await _tenantAdmin.Received(1).RefreshTenantConfigurationAsync(Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private const string ValidMappingJson = """
        {
          "mappingId": "custom-v1",
          "eventType": "CustomEvent",
          "fieldMappings": { "Name": { "sourceType": "DirectField", "sourceFieldId": "name" } }
        }
        """;

    private void SetupAllowedTemplate(Guid templateGuid)
    {
        _templates.GetAccessibleTemplatesAsync(Arg.Any<CancellationToken>()).Returns([
            new TemplateDto { TemplateId = templateGuid, Name = "Transfers", CreatedOn = DateTime.UtcNow }
        ]);
        _state.SelectedTemplateId = templateGuid.ToString();
    }

    private void SetupSchemaAlias(Guid templateGuid, string jsonSchema) =>
        _templates.GetLatestTemplateSchemaAsync(templateGuid, Arg.Any<CancellationToken>())
            .Returns(new TemplateSchemaDto
            {
                TemplateId = templateGuid,
                TemplateVersionId = Guid.NewGuid(),
                VersionNumber = "1.0.0",
                JsonSchema = jsonSchema
            });

    private void SetupCatalogue(params (string EventTypeName, string Kind, string[] Properties)[] events) =>
        _tenantAdmin.GetEventCatalogueAsync(Arg.Any<CancellationToken>())
            .Returns(new GetEventCatalogueResponse
            {
                Events = events
                    .Select(e => new EventCatalogueItemDto
                    {
                        EventTypeName = e.EventTypeName,
                        TopicName = $"{e.EventTypeName}-topic",
                        ClrTypeName = e.EventTypeName,
                        Version = "1.0",
                        Kind = e.Kind,
                        Properties = e.Properties
                            .Select(p => new EventCataloguePropertyDto { Name = p })
                            .ToList()
                    })
                    .ToList()
            });

    private void SetupSettings(params TenantSettingDto[] settings) =>
        _tenantAdmin.GetSafeTenantSettingsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new GetTenantSettingsResponse(_state.TenantId, "Transfers", settings));

    private static TenantSettingDto TenantSetting(string category, string settingsJson) =>
        new(
            Guid.NewGuid(),
            category,
            EventMappingsAdminService.TargetShared,
            settingsJson,
            false,
            DateTime.UtcNow);

    private JsonObject CapturedPayload()
    {
        var request = (UpsertTenantSettingRequest)_tenantAdmin.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(ITenantAdminClient.UpsertSafeTenantSettingAsync))
            .GetArguments()[1]!;

        return (JsonObject)JsonNode.Parse(
            Encoding.UTF8.GetString(Convert.FromBase64String(request.SettingsJson)))!;
    }
}
