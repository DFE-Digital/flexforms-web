using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

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
}
