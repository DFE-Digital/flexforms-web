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
    }

    [Fact]
    public void SuggestNextVersion_ShouldPreferLatestVersion()
    {
        Assert.Equal("1.0.3", _service.SuggestNextVersion("1.0.2", "1.0.0"));
    }
}
