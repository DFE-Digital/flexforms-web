using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Services;

public class ContributorPatternServiceTests
{
    [Fact]
    public async Task IsEnabledAsync_ShouldReturnTemplateFlag()
    {
        var templates = Substitute.For<ITemplateManagementService>();
        templates.LoadTemplateAsync("tpl", Arg.Any<ApplicationDto?>())
            .Returns(new FormTemplate
            {
                TemplateId = "tpl",
                TemplateName = "tpl",
                Description = "tpl",
                ContributorPattern = true,
                TaskGroups = []
            });

        var enabled = await new ContributorPatternService(templates).IsEnabledAsync("tpl");

        Assert.True(enabled);
    }
}

public class ComplexFieldConfigurationServiceTests
{
    [Fact]
    public void GetConfiguration_ShouldBindArrayThenFallbackToMissingDefaults()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FormEngine:ComplexFields:0:Id"] = "school",
            ["FormEngine:ComplexFields:0:FieldType"] = "autocomplete",
            ["FormEngine:ComplexFields:0:ApiEndpoint"] = "https://example.test/search"
        }).Build();
        var requestConfig = Substitute.For<IRequestAppConfiguration>();
        requestConfig.Current.Returns(configuration);
        var service = new ComplexFieldConfigurationService(
            requestConfig,
            NullLogger<ComplexFieldConfigurationService>.Instance);

        var found = service.GetConfiguration("school");
        Assert.Equal("https://example.test/search", found.ApiEndpoint);

        var missing = service.GetConfiguration("unknown");
        Assert.Equal("unknown", missing.Id);
        Assert.Equal("autocomplete", missing.FieldType);
    }

    [Fact]
    public void GetConfiguration_ShouldBindClientCredentialsAndSkipApiKeyFallback()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FormEngine:ComplexFields:0:Id"] = "orgs",
            ["FormEngine:ComplexFields:0:FieldType"] = "autocomplete",
            ["FormEngine:ComplexFields:0:ApiEndpoint"] = "https://example.test/orgs",
            ["FormEngine:ComplexFields:0:AuthType"] = "ClientCredentials",
            ["FormEngine:ComplexFields:0:TokenEndpoint"] = "https://login.example.test/token",
            ["FormEngine:ComplexFields:0:ClientId"] = "client-id",
            ["FormEngine:ComplexFields:0:ClientSecret"] = "client-secret",
            ["FormEngine:ComplexFields:0:Scope"] = "api://example/.default",
            ["FormEngine:ComplexFields:1:Id"] = "school",
            ["FormEngine:ComplexFields:1:ApiKey"] = "shared-key",
            ["FormEngine:AcademiesApiKey"] = "fallback-key"
        }).Build();
        var requestConfig = Substitute.For<IRequestAppConfiguration>();
        requestConfig.Current.Returns(configuration);
        var service = new ComplexFieldConfigurationService(
            requestConfig,
            NullLogger<ComplexFieldConfigurationService>.Instance);

        var found = service.GetConfiguration("orgs");

        Assert.True(found.UsesClientCredentials);
        Assert.Equal("https://login.example.test/token", found.TokenEndpoint);
        Assert.Equal("client-id", found.ClientId);
        Assert.Equal("client-secret", found.ClientSecret);
        Assert.Equal("api://example/.default", found.Scope);
        Assert.True(string.IsNullOrEmpty(found.ApiKey));
    }

    [Fact]
    public void GetConfiguration_ShouldBindDropdownAndConfirmationDisplay()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FormEngine:ComplexFields:0:Id"] = "member",
            ["FormEngine:ComplexFields:0:FieldType"] = "autocomplete",
            ["FormEngine:ComplexFields:0:ApiEndpoint"] = "https://example.test/members",
            ["FormEngine:ComplexFields:0:DropdownDisplay"] = "displayName + \" - \" + constituencyName",
            ["FormEngine:ComplexFields:0:ConfirmationDisplay"] = "firstName + \" \" + lastName"
        }).Build();
        var requestConfig = Substitute.For<IRequestAppConfiguration>();
        requestConfig.Current.Returns(configuration);
        var service = new ComplexFieldConfigurationService(
            requestConfig,
            NullLogger<ComplexFieldConfigurationService>.Instance);

        var found = service.GetConfiguration("member");

        Assert.Equal("displayName + \" - \" + constituencyName", found.DropdownDisplay);
        Assert.Equal("firstName + \" \" + lastName", found.ConfirmationDisplay);
    }
}
