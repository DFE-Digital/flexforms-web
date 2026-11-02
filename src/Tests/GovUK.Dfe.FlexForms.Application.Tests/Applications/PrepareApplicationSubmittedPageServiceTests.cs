using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Applications;
using GovUK.Dfe.FlexForms.Application.DisplayCopy;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GovUK.Dfe.FlexForms.Application.Tests.Applications;

public class PrepareApplicationSubmittedPageServiceTests
{
    private readonly IApplicationsClient _applications = Substitute.For<IApplicationsClient>();
    private readonly IApplicationTerminologyProvider _terminology = Substitute.For<IApplicationTerminologyProvider>();

    public PrepareApplicationSubmittedPageServiceTests()
    {
        _terminology.Singular.Returns("plan");
        _terminology.SingularCapitalised.Returns("Plan");
    }

    [Fact]
    public async Task ExecuteAsync_UsesPlatformDefaults_WhenNoConfig()
    {
        var service = CreateService(new ConfigurationBuilder().Build());
        var state = new ApplicationSubmittedWorkState { ReferenceNumber = "TRF-1" };

        await service.ExecuteAsync(state);

        Assert.Equal("Plan submitted", state.PanelTitle);
        Assert.Contains("What happens next", state.BodyMarkdown);
        Assert.Contains("your plan", state.BodyMarkdown);
    }

    [Fact]
    public async Task ExecuteAsync_UsesTenantTemplateCopy_WhenGuidMatches()
    {
        var templateId = Guid.NewGuid();
        GivenApplication("TRF-1", templateId, """{"templateId":"form-001"}""");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ApplicationSubmittedPage:{templateId}:PanelTitle"] = "Transfer submitted",
                [$"ApplicationSubmittedPage:{templateId}:BodyMarkdown"] = "Custom body"
            })
            .Build();

        var service = CreateService(config);
        var state = new ApplicationSubmittedWorkState { ReferenceNumber = "TRF-1" };

        await service.ExecuteAsync(state);

        Assert.Equal("Transfer submitted", state.PanelTitle);
        Assert.Equal("Custom body", state.BodyMarkdown);
    }

    [Fact]
    public async Task ExecuteAsync_UsesSchemaAlias_WhenGuidHasNoCopy()
    {
        var templateId = Guid.NewGuid();
        GivenApplication("TRF-1", templateId, """{"templateId":"form-001"}""");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApplicationSubmittedPage:form-001:PanelTitle"] = "Alias title",
                ["ApplicationSubmittedPage:form-001:BodyMarkdown"] = "Alias body"
            })
            .Build();

        var service = CreateService(config);
        var state = new ApplicationSubmittedWorkState { ReferenceNumber = "TRF-1" };

        await service.ExecuteAsync(state);

        Assert.Equal("Alias title", state.PanelTitle);
        Assert.Equal("Alias body", state.BodyMarkdown);
    }

    [Fact]
    public async Task ExecuteAsync_UsesDefaultKey_WhenTemplateLookupFails()
    {
        _applications.GetApplicationByReferenceAsync("MISSING", Arg.Any<CancellationToken>())
            .Throws(new ExternalApplicationsException("missing", 404, "err", null!, null!));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApplicationSubmittedPage:_default:PanelTitle"] = "Fallback title",
                ["ApplicationSubmittedPage:_default:BodyMarkdown"] = "Fallback body"
            })
            .Build();

        var service = CreateService(config);
        var state = new ApplicationSubmittedWorkState { ReferenceNumber = "MISSING" };

        await service.ExecuteAsync(state);

        Assert.Equal("Fallback title", state.PanelTitle);
        Assert.Equal("Fallback body", state.BodyMarkdown);
    }

    [Fact]
    public async Task ExecuteAsync_UsesTemplateDisplayCopy_WhenSubmittedPageIsOnTheTemplate()
    {
        var templateId = Guid.NewGuid();
        GivenApplication("TRF-1", templateId, SchemaWithSubmittedPage("From the template", "Template body for {singular}"));

        var service = CreateService(new ConfigurationBuilder().Build());
        var state = new ApplicationSubmittedWorkState { ReferenceNumber = "TRF-1" };

        await service.ExecuteAsync(state);

        Assert.Equal("From the template", state.PanelTitle);
        Assert.Equal("Template body for plan", state.BodyMarkdown);
    }

    [Fact]
    public async Task ExecuteAsync_PrefersTemplateDisplayCopy_OverTenantTemplateCopy()
    {
        var templateId = Guid.NewGuid();
        GivenApplication("TRF-1", templateId, SchemaWithSubmittedPage("From the template", "Template body"));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ApplicationSubmittedPage:{templateId}:PanelTitle"] = "From tenant settings",
                [$"ApplicationSubmittedPage:{templateId}:BodyMarkdown"] = "Tenant body",
                ["ApplicationSubmittedPage:_default:PanelTitle"] = "Default title",
                ["ApplicationSubmittedPage:_default:BodyMarkdown"] = "Default body"
            })
            .Build();

        var service = CreateService(config);
        var state = new ApplicationSubmittedWorkState { ReferenceNumber = "TRF-1" };

        await service.ExecuteAsync(state);

        Assert.Equal("From the template", state.PanelTitle);
        Assert.Equal("Template body", state.BodyMarkdown);
    }

    [Fact]
    public async Task ExecuteAsync_FallsBackToTenantCopy_WhenDisplayCopyFieldsAreBlank()
    {
        var templateId = Guid.NewGuid();
        GivenApplication("TRF-1", templateId, SchemaWithSubmittedPage("", ""));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ApplicationSubmittedPage:{templateId}:PanelTitle"] = "Tenant title",
                [$"ApplicationSubmittedPage:{templateId}:BodyMarkdown"] = "Tenant body"
            })
            .Build();

        var service = CreateService(config);
        var state = new ApplicationSubmittedWorkState { ReferenceNumber = "TRF-1" };

        await service.ExecuteAsync(state);

        Assert.Equal("Tenant title", state.PanelTitle);
        Assert.Equal("Tenant body", state.BodyMarkdown);
    }

    [Fact]
    public async Task ExecuteAsync_UsesTenantField_WhenOnlyOneDisplayCopyFieldIsSet()
    {
        var templateId = Guid.NewGuid();
        GivenApplication("TRF-1", templateId, SchemaWithSubmittedPage("Template title", ""));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApplicationSubmittedPage:_default:PanelTitle"] = "Default title",
                ["ApplicationSubmittedPage:_default:BodyMarkdown"] = "Default body"
            })
            .Build();

        var service = CreateService(config);
        var state = new ApplicationSubmittedWorkState { ReferenceNumber = "TRF-1" };

        await service.ExecuteAsync(state);

        Assert.Equal("Template title", state.PanelTitle);
        Assert.Equal("Default body", state.BodyMarkdown);
    }

    private void GivenApplication(string reference, Guid templateId, string jsonSchema)
    {
        _applications.GetApplicationByReferenceAsync(reference, Arg.Any<CancellationToken>())
            .Returns(new ApplicationDto
            {
                ApplicationReference = reference,
                TemplateSchema = new TemplateSchemaDto
                {
                    TemplateId = templateId,
                    TemplateVersionId = Guid.NewGuid(),
                    VersionNumber = "1",
                    JsonSchema = jsonSchema
                }
            });
    }

    private static string SchemaWithSubmittedPage(string panelTitle, string bodyMarkdown) =>
        $$"""
        {
          "templateId": "form-001",
          "templateName": "Transfers",
          "description": "Transfers",
          "taskGroups": [],
          "displayCopy": {
            "submittedPage": {
              "panelTitle": {{JsonString(panelTitle)}},
              "bodyMarkdown": {{JsonString(bodyMarkdown)}}
            }
          }
        }
        """;

    private static string JsonString(string value) =>
        $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    private PrepareApplicationSubmittedPageService CreateService(IConfiguration configuration) =>
        new(
            _applications,
            new TestRequestConfig(configuration),
            _terminology,
            new TemplateCopyContext(),
            NullLogger<PrepareApplicationSubmittedPageService>.Instance);

    private sealed class TestRequestConfig(IConfiguration current) : IRequestAppConfiguration
    {
        public string? this[string key] => current[key];

        public IConfigurationSection GetSection(string key) => current.GetSection(key);

        public IConfiguration Current => current;
    }
}
