using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Applications;
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
    public async Task ExecuteAsync_UsesTemplateCopy_WhenGuidMatches()
    {
        var templateId = Guid.NewGuid();
        _applications.GetApplicationByReferenceAsync("TRF-1", Arg.Any<CancellationToken>())
            .Returns(new ApplicationDto
            {
                ApplicationReference = "TRF-1",
                TemplateSchema = new TemplateSchemaDto
                {
                    TemplateId = templateId,
                    TemplateVersionId = Guid.NewGuid(),
                    VersionNumber = "1",
                    JsonSchema = """{"templateId":"form-001"}"""
                }
            });

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
        _applications.GetApplicationByReferenceAsync("TRF-1", Arg.Any<CancellationToken>())
            .Returns(new ApplicationDto
            {
                ApplicationReference = "TRF-1",
                TemplateSchema = new TemplateSchemaDto
                {
                    TemplateId = templateId,
                    TemplateVersionId = Guid.NewGuid(),
                    VersionNumber = "1",
                    JsonSchema = """{"templateId":"form-001"}"""
                }
            });

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

    private PrepareApplicationSubmittedPageService CreateService(IConfiguration configuration) =>
        new(
            _applications,
            new TestRequestConfig(configuration),
            _terminology,
            NullLogger<PrepareApplicationSubmittedPageService>.Instance);

    private sealed class TestRequestConfig(IConfiguration current) : IRequestAppConfiguration
    {
        public string? this[string key] => current[key];

        public IConfigurationSection GetSection(string key) => current.GetSection(key);

        public IConfiguration Current => current;
    }
}
