using System.Collections.ObjectModel;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Templates;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GovUK.Dfe.FlexForms.Application.Tests.Admin;

public class TemplateManagerAdminServiceTests
{
    private readonly ITemplatesClient _templates = Substitute.For<ITemplatesClient>();
    private readonly ITemplateValidationService _validation = Substitute.For<ITemplateValidationService>();
    private readonly TemplateManagerAdminService _service;

    public TemplateManagerAdminServiceTests()
    {
        _validation.ValidateTemplateJson(Arg.Any<string>()).Returns((true, new List<string>()));
        _service = new TemplateManagerAdminService(
            _templates,
            _validation,
            NullLogger<TemplateManagerAdminService>.Instance);
    }

    [Fact]
    public void ValidateNewVersion_ShouldStayWithErrors_WhenRequiredFieldsAreMissing()
    {
        var state = new TemplateManagerWorkState();

        var result = _service.ValidateNewVersion(state);

        Assert.Equal(AdminPageOutcomeKind.StayOnPage, result.Kind);
        Assert.Contains(result.Errors, e => e.Message == TemplateManagerMessages.VersionRequired);
        Assert.Contains(result.Errors, e => e.Message == TemplateManagerMessages.SchemaRequired);
        Assert.Contains(result.Errors, e => e.Message == TemplateManagerMessages.AcknowledgeReportingImpact);
    }

    [Fact]
    public void ValidateNewVersion_ShouldStay_WhenSchemaValidationFails()
    {
        _validation.ValidateTemplateJson(Arg.Any<string>()).Returns((false, ["schema broken"]));
        var state = new TemplateManagerWorkState
        {
            NewVersion = "1.0.2",
            NewSchema = "{}",
            AcknowledgeReportingImpact = true
        };

        var result = _service.ValidateNewVersion(state);

        Assert.Contains(result.Errors, e => e.Message == "schema broken");
        Assert.Contains(result.Errors, e => e.FieldKey == nameof(TemplateManagerWorkState.NewSchema));
    }

    [Fact]
    public void PrefillNewSchemaIfEmpty_ShouldKeepPostedSchema()
    {
        var posted = """{ "name": "broken" }""";
        var state = new TemplateManagerWorkState
        {
            ShowAddVersionForm = true,
            NewSchema = posted,
            CurrentTemplateJson = """{ "name": "current" }"""
        };

        _service.PrefillNewSchemaIfEmpty(state, Guid.NewGuid());

        Assert.Equal(posted, state.NewSchema);
    }

    [Fact]
    public async Task CreateVersionAsync_ShouldStayWithError_WhenApiRejectsSchema()
    {
        var templateId = Guid.NewGuid();
        _templates.CreateTemplateVersionAsync(
                templateId,
                Arg.Any<CreateTemplateVersionRequest>(),
                Arg.Any<CancellationToken>())
            .Returns<TemplateSchemaDto>(_ => throw new InvalidOperationException("Version already exists"));

        var result = await _service.CreateVersionAsync(
            new TemplateManagerWorkState { NewVersion = "1.0.2", NewSchema = "{}" },
            templateId);

        Assert.Equal(AdminPageOutcomeKind.StayOnPage, result.Kind);
        Assert.Contains(result.Errors, e =>
            e.FieldKey == nameof(TemplateManagerWorkState.NewSchema)
            && e.Message == TemplateManagerMessages.SaveFailed);
    }

    [Fact]
    public void SuggestNextVersion_ShouldPreferLatestVersion()
    {
        Assert.Equal("1.0.3", _service.SuggestNextVersion("1.0.2", "1.0.0"));
    }

    [Fact]
    public async Task GrantToAllUsersAsync_ShouldReturnGrantedSummary()
    {
        var templateId = Guid.NewGuid();
        _templates.GrantTemplateAccessToAllUsersAsync(templateId, Arg.Any<CancellationToken>())
            .Returns(new GrantTemplateAccessToAllUsersResponse(templateId, 5, 3, 2));

        var state = new TemplateManagerWorkState();
        var result = await _service.GrantToAllUsersAsync(state, templateId);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal(TemplateManagerMessages.GrantedSummary(3, 2, 5), result.SuccessMessage);
        Assert.Equal(TemplateManagerMessages.GrantedSummary(3, 2, 5), state.GrantToAllUsersSummary);
    }

    [Fact]
    public async Task LoadTemplateDataAsync_ShouldLoadSelectedVersion()
    {
        var templateId = Guid.NewGuid();
        var template = new TemplateDto { TemplateId = templateId, Name = "Transfers", CreatedOn = DateTime.UtcNow };
        var versionId = Guid.NewGuid();
        _templates.GetTemplateVersionsAsync(templateId, Arg.Any<CancellationToken>())
            .Returns(new ObservableCollection<TemplateVersionSummaryDto>
            {
                new()
                {
                    TemplateId = templateId,
                    TemplateVersionId = versionId,
                    VersionNumber = "1.0.1",
                    CreatedOn = DateTime.UtcNow
                },
                new()
                {
                    TemplateId = templateId,
                    TemplateVersionId = Guid.NewGuid(),
                    VersionNumber = "1.0.0",
                    CreatedOn = DateTime.UtcNow.AddDays(-1)
                }
            });
        _templates.GetTemplateSchemaByVersionAsync(templateId, "1.0.1", Arg.Any<CancellationToken>())
            .Returns(new TemplateSchemaDto
            {
                TemplateId = templateId,
                TemplateVersionId = versionId,
                VersionNumber = "1.0.1",
                JsonSchema = StarterFormTemplateSchema.CreateJson(templateId.ToString(), "Transfers")
            });

        var state = new TemplateManagerWorkState { TenantTemplates = [template] };
        await _service.LoadTemplateDataAsync(state, templateId);

        Assert.Equal("1.0.1", state.CurrentVersionNumber);
        Assert.NotNull(state.CurrentTemplate);
        Assert.Equal("Transfers", state.CurrentTemplate.TemplateName);
        Assert.False(state.HasError);
    }

    [Fact]
    public async Task LoadTemplateDataAsync_ShouldClearSchema_WhenNoVersionsExist()
    {
        var templateId = Guid.NewGuid();
        var template = new TemplateDto { TemplateId = templateId, Name = "Transfers", CreatedOn = DateTime.UtcNow };
        _templates.GetTemplateVersionsAsync(templateId, Arg.Any<CancellationToken>())
            .Returns(new ObservableCollection<TemplateVersionSummaryDto>());

        var state = new TemplateManagerWorkState { TenantTemplates = [template] };
        await _service.LoadTemplateDataAsync(state, templateId);

        Assert.Null(state.CurrentTemplate);
        Assert.Null(state.CurrentVersionNumber);
    }

    [Fact]
    public async Task LoadTemplateDataAsync_ShouldSetError_WhenApiThrows()
    {
        var templateId = Guid.NewGuid();
        var template = new TemplateDto { TemplateId = templateId, Name = "Transfers", CreatedOn = DateTime.UtcNow };
        _templates.GetTemplateVersionsAsync(templateId, Arg.Any<CancellationToken>())
            .Returns<ObservableCollection<TemplateVersionSummaryDto>>(_ => throw new InvalidOperationException("down"));

        var state = new TemplateManagerWorkState { TenantTemplates = [template] };
        await _service.LoadTemplateDataAsync(state, templateId);

        Assert.True(state.HasError);
        Assert.Equal(TemplateManagerMessages.LoadFailed, state.ErrorMessage);
    }

    [Fact]
    public void ValidateNewVersion_ShouldStayWithoutErrors_WhenInputIsValid()
    {
        var state = new TemplateManagerWorkState
        {
            NewVersion = "1.0.2",
            NewSchema = StarterFormTemplateSchema.CreateJson(Guid.NewGuid().ToString(), "Transfers"),
            AcknowledgeReportingImpact = true
        };

        var result = _service.ValidateNewVersion(state);

        Assert.Equal(AdminPageOutcomeKind.StayOnPage, result.Kind);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task CreateVersionAsync_ShouldRedirect_WhenApiSucceeds()
    {
        var templateId = Guid.NewGuid();
        _templates.CreateTemplateVersionAsync(
                templateId,
                Arg.Any<CreateTemplateVersionRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(new TemplateSchemaDto
            {
                TemplateId = templateId,
                TemplateVersionId = Guid.NewGuid(),
                VersionNumber = "1.0.2",
                JsonSchema = "{}"
            });

        var result = await _service.CreateVersionAsync(
            new TemplateManagerWorkState { NewVersion = "1.0.2", NewSchema = "{}" },
            templateId);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.True(result.RouteValues!.ContainsKey("success"));
    }

    [Fact]
    public void PrefillNewSchemaIfEmpty_ShouldCopyCurrentTemplateJson()
    {
        var state = new TemplateManagerWorkState
        {
            ShowAddVersionForm = true,
            CurrentTemplateJson = """{ "name": "current" }"""
        };

        _service.PrefillNewSchemaIfEmpty(state, Guid.NewGuid());

        Assert.Equal("""{ "name": "current" }""", state.NewSchema);
    }

    [Fact]
    public async Task GrantToAllUsersAsync_ShouldStay_WhenApiFails()
    {
        var templateId = Guid.NewGuid();
        var template = new TemplateDto { TemplateId = templateId, Name = "Transfers", CreatedOn = DateTime.UtcNow };
        _templates.GrantTemplateAccessToAllUsersAsync(templateId, Arg.Any<CancellationToken>())
            .Throws(new ExternalApplicationsException("boom", 500, "err", null!, null!));
        _templates.GetTemplateVersionsAsync(templateId, Arg.Any<CancellationToken>())
            .Returns(new ObservableCollection<TemplateVersionSummaryDto>());

        var state = new TemplateManagerWorkState { TenantTemplates = [template] };
        var result = await _service.GrantToAllUsersAsync(state, templateId);

        Assert.Equal(AdminPageOutcomeKind.StayOnPage, result.Kind);
        Assert.Equal(TemplateManagerMessages.GrantFailed, result.ErrorMessage);
        Assert.True(state.HasError);
    }
}