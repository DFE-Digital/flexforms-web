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
}
