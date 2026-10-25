using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Validation;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Web.Pages.Admin;
using GovUK.Dfe.FlexForms.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages.Admin;

public class TemplateStudioModelTests
{
    private const string StoredSchema = """{ "templateId": "tpl", "templateName": "Transfers", "semanticKeyKeptHere": true }""";

    private readonly ITemplateManagerAdmin _admin = Substitute.For<ITemplateManagerAdmin>();
    private readonly ITemplateSelectionService _selection = Substitute.For<ITemplateSelectionService>();
    private readonly ITemplateValidationService _validation = Substitute.For<ITemplateValidationService>();
    private readonly ITemplateCacheInvalidator _cacheInvalidator = Substitute.For<ITemplateCacheInvalidator>();
    private readonly IComplexFieldConfigurationService _complexFields = Substitute.For<IComplexFieldConfigurationService>();
    private readonly IApplicationsClient _applications = Substitute.For<IApplicationsClient>();
    private readonly InMemorySession _session = new();
    private readonly TemplateDto _template = new() { TemplateId = Guid.NewGuid(), Name = "Transfers", CreatedOn = DateTime.UtcNow };
    private readonly TemplateStudioModel _model;

    public TemplateStudioModelTests()
    {
        _model = new TemplateStudioModel(
            _admin, _selection, _validation, _cacheInvalidator, _complexFields, _applications, NullLogger<TemplateStudioModel>.Instance);
        GivenApplicationCount(Task.FromResult(new PagedResultOfApplicationDto { TotalCount = 4 }));

        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<ISessionFeature>(new StubSessionFeature { Session = _session });
        _model.PageContext = new PageContext
        {
            HttpContext = httpContext,
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        };
        var url = Substitute.For<IUrlHelper>();
        url.ActionContext.Returns(new ActionContext(httpContext, new RouteData(), new ActionDescriptor()));
        url.RouteUrl(Arg.Any<UrlRouteContext>()).Returns("/admin/template-studio?handler=Validate");
        _model.Url = url;

        _selection.GetSelectableTemplatesAsync(Arg.Any<CancellationToken>()).Returns([_template]);
        _selection.GetSelectedTemplateId(Arg.Any<HttpContext>()).Returns(_template.TemplateId.ToString());
        _admin.SuggestNextVersion("1.0.3", "1.0.3").Returns("1.0.4");
        _complexFields.GetAllConfigurations().Returns(
        [
            new ComplexFieldConfiguration { Id = "TrustComplexField", FieldType = "autocomplete", ApiKey = "api-key-value", ClientSecret = "client-secret-value" }
        ]);
    }

    [Fact]
    public async Task OnGetAsync_ShouldRedirectToTemplateManager_WhenNoTemplateIsSelected()
    {
        _selection.GetSelectedTemplateId(Arg.Any<HttpContext>()).Returns((string?)null);

        var result = await _model.OnGetAsync(null, CancellationToken.None);

        Assert.Equal("/Admin/TemplateManager", Assert.IsType<RedirectToPageResult>(result).PageName);
    }

    [Fact]
    public async Task OnGetAsync_ShouldEmbedTheStoredSchemaAndSuggestTheNextVersion_WithoutComplexFieldSecrets()
    {
        GivenStoredVersion("1.0.3");

        var result = await _model.OnGetAsync(null, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("1.0.4", _model.NewVersion);
        using var data = JsonDocument.Parse(_model.StudioDataJson);
        Assert.Equal(StoredSchema, data.RootElement.GetProperty("schemaJson").GetString());
        Assert.False(data.RootElement.GetProperty("isStarter").GetBoolean());
        Assert.Equal("TrustComplexField", data.RootElement.GetProperty("complexFields")[0].GetProperty("id").GetString());
        Assert.DoesNotContain("api-key-value", _model.StudioDataJson);
        Assert.DoesNotContain("client-secret-value", _model.StudioDataJson);
    }

    [Fact]
    public async Task OnGetAsync_ShouldStartFromTheStarterTemplate_WhenThereAreNoVersions()
    {
        await _model.OnGetAsync(null, CancellationToken.None);

        Assert.Equal("1.0.0", _model.NewVersion);
        using var data = JsonDocument.Parse(_model.StudioDataJson);
        Assert.True(data.RootElement.GetProperty("isStarter").GetBoolean());
        Assert.Contains("starter-group", data.RootElement.GetProperty("schemaJson").GetString());
        Assert.Equal(0, data.RootElement.GetProperty("applicationCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, data.RootElement.GetProperty("baselineSchemaJson").ValueKind);
        await _applications.DidNotReceiveWithAnyArgs().GetApplicationsByTemplateAsync(default);
    }

    [Fact]
    public async Task OnGetAsync_ShouldTellTemplateStudioHowManyApplicationsUseTheTemplate_AndWhatTheyWereSavedAgainst()
    {
        GivenStoredVersion("1.0.3");

        await _model.OnGetAsync(null, CancellationToken.None);

        using var data = JsonDocument.Parse(_model.StudioDataJson);
        Assert.Equal(4, data.RootElement.GetProperty("applicationCount").GetInt32());
        Assert.Equal(StoredSchema, data.RootElement.GetProperty("baselineSchemaJson").GetString());
        await _applications.Received(1).GetApplicationsByTemplateAsync(
            _template.TemplateId, false, 1, 1, Arg.Any<string?>(), Arg.Any<DateTime?>(), Arg.Any<DateTime?>(),
            Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<ApplicationStatus?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnGetAsync_ShouldLeaveTheApplicationCountUnknown_WhenItCannotBeChecked()
    {
        GivenStoredVersion("1.0.3");
        GivenApplicationCount(Task.FromException<PagedResultOfApplicationDto>(new HttpRequestException("forbidden")));

        await _model.OnGetAsync(null, CancellationToken.None);

        using var data = JsonDocument.Parse(_model.StudioDataJson);
        Assert.Equal(JsonValueKind.Null, data.RootElement.GetProperty("applicationCount").ValueKind);
    }

    [Fact]
    public async Task OnPostSaveAsync_ShouldCompareWithTheStoredVersion_NotTheEditedOne_WhenRedisplaying()
    {
        GivenStoredVersion("1.0.3");
        _model.NewVersion = "1.0.4";
        _model.NewSchema = """{ "edited": true }""";
        _admin.ValidateNewVersion(Arg.Any<TemplateManagerWorkState>())
            .Returns(AdminPageOutcome.Stay(errors: [new FormValidationError("NewSchema", "problem")]));

        await _model.OnPostSaveAsync(CancellationToken.None);

        using var data = JsonDocument.Parse(_model.StudioDataJson);
        Assert.Equal(StoredSchema, data.RootElement.GetProperty("baselineSchemaJson").GetString());
    }

    private void GivenApplicationCount(Task<PagedResultOfApplicationDto> result) =>
        _applications.GetApplicationsByTemplateAsync(
                Arg.Any<Guid>(), Arg.Any<bool?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<string?>(),
                Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<DateTime?>(),
                Arg.Any<ApplicationStatus?>(), Arg.Any<CancellationToken>())
            .Returns(result);

    [Fact]
    public async Task OnGetAsync_ShouldNotOfferTheStarterTemplate_WhenTheTemplateFailedToLoad()
    {
        _admin.LoadTemplateDataAsync(Arg.Any<TemplateManagerWorkState>(), _template.TemplateId, Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var state = call.Arg<TemplateManagerWorkState>();
                state.HasError = true;
                state.ErrorMessage = TemplateManagerMessages.LoadFailed;
                return Task.CompletedTask;
            });

        await _model.OnGetAsync(null, CancellationToken.None);

        Assert.Equal(TemplateManagerMessages.LoadFailed, _model.LoadError);
        Assert.Equal("{}", _model.StudioDataJson);
    }

    [Fact]
    public async Task OnPostSaveAsync_ShouldKeepTheEditedSchemaAndShowErrors_WhenValidationFails()
    {
        GivenStoredVersion("1.0.3");
        _model.NewVersion = "1.0.4";
        _model.NewSchema = """{ "edited": true }""";
        _admin.ValidateNewVersion(Arg.Any<TemplateManagerWorkState>())
            .Returns(AdminPageOutcome.Stay(errors: [new FormValidationError("NewSchema", "Template must have a 'templateId'")]));

        var result = await _model.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Template must have a 'templateId'", _model.ModelState["NewSchema"]!.Errors.Single().ErrorMessage);
        using var data = JsonDocument.Parse(_model.StudioDataJson);
        Assert.Equal("""{ "edited": true }""", data.RootElement.GetProperty("schemaJson").GetString());
        Assert.True(data.RootElement.GetProperty("hasUnsavedChanges").GetBoolean());
        await _admin.DidNotReceiveWithAnyArgs().CreateVersionAsync(default!, default, default);
    }

    [Fact]
    public async Task OnPostSaveAsync_ShouldCreateTheVersionClearTheCacheAndRememberTheVersion()
    {
        GivenStoredVersion("1.0.3");
        _model.NewVersion = " 1.0.4 ";
        _model.NewSchema = StoredSchema;
        _model.AcknowledgeReportingImpact = true;
        _admin.ValidateNewVersion(Arg.Any<TemplateManagerWorkState>()).Returns(AdminPageOutcome.Stay());
        _admin.CreateVersionAsync(Arg.Any<TemplateManagerWorkState>(), _template.TemplateId, Arg.Any<CancellationToken>())
            .Returns(AdminPageOutcome.Redirect());

        var result = await _model.OnPostSaveAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("1.0.4", redirect.RouteValues!["saved"]);
        await _admin.Received(1).CreateVersionAsync(
            Arg.Is<TemplateManagerWorkState>(state =>
                state.NewVersion == "1.0.4" && state.NewSchema == StoredSchema && state.AcknowledgeReportingImpact),
            _template.TemplateId,
            Arg.Any<CancellationToken>());
        await _cacheInvalidator.Received(1).InvalidateAsync(_template.TemplateId.ToString());
        Assert.True(_session.TryGetValue("TemplateVersionNumber", out var stored));
        Assert.Equal("1.0.4", Encoding.UTF8.GetString(stored));
    }

    [Fact]
    public void OnPostValidate_ShouldReturnTheSaveTimeChecks()
    {
        _validation.ValidateTemplateJson("{}").Returns((false, new List<string> { "Template must have a 'templateId'" }));

        var result = Assert.IsType<JsonResult>(_model.OnPostValidate(new TemplateStudioValidateRequest("{}")));

        var json = JsonSerializer.Serialize(result.Value);
        Assert.Equal("""{"isValid":false,"errors":["Template must have a \u0027templateId\u0027"]}""", json);
    }

    private void GivenStoredVersion(string version) =>
        _admin.LoadTemplateDataAsync(Arg.Any<TemplateManagerWorkState>(), _template.TemplateId, Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var state = call.Arg<TemplateManagerWorkState>();
                state.AvailableVersions = [new TemplateVersionSummaryDto { TemplateId = _template.TemplateId, TemplateVersionId = Guid.NewGuid(), VersionNumber = version, CreatedOn = DateTime.UtcNow }];
                state.CurrentVersionNumber = version;
                state.LatestVersionNumber = version;
                state.CurrentTemplateJson = StoredSchema;
                return Task.CompletedTask;
            });

    private sealed class StubSessionFeature : ISessionFeature
    {
        public ISession Session { get; set; } = new InMemorySession();
    }

    private sealed class InMemorySession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = [];

        public bool IsAvailable => true;

        public string Id => "test-session";

        public IEnumerable<string> Keys => _store.Keys;

        public void Clear() => _store.Clear();

        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Remove(string key) => _store.Remove(key);

        public void Set(string key, byte[] value) => _store[key] = value;

        public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value)
            => _store.TryGetValue(key, out value);
    }
}
