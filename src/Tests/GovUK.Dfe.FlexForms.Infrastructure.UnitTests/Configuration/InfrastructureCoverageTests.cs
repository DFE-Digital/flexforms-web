using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Infrastructure.Configuration;
using GovUK.Dfe.FlexForms.Infrastructure.Messaging;
using GovUK.Dfe.FlexForms.Infrastructure.Providers;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Configuration;

public class ConfigurationSectionJsonTests
{
    [Fact]
    public void ToJson_ShouldReturnNull_WhenSectionIsEmpty()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        Assert.Null(ConfigurationSectionJson.ToJson(configuration.GetSection("Missing")));
    }

    [Fact]
    public void ToJson_ShouldSerializeObjectArrayAndLeaves()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Root:Name"] = "Transfers",
            ["Root:Enabled"] = "true",
            ["Root:Count"] = "2",
            ["Root:Items:0"] = "a",
            ["Root:Items:1"] = "b"
        }).Build();

        var json = ConfigurationSectionJson.ToJson(configuration.GetSection("Root"));

        Assert.Contains("\"Name\":\"Transfers\"", json);
        Assert.Contains("\"Enabled\":true", json);
        Assert.Contains("\"Count\":2", json);
        Assert.Contains("[\"a\",\"b\"]", json);
    }
}

public class ScanEventRoutingTests
{
    [Fact]
    public void GetMetadata_ShouldReadStringAndGuid()
    {
        var id = Guid.NewGuid();
        var metadata = new Dictionary<string, object>
        {
            ["templateId"] = id.ToString(),
            ["originalFileName"] = "a.pdf"
        };

        Assert.Equal("a.pdf", ScanEventRouting.GetMetadata(metadata, ScanEventRouting.OriginalFileNameMetadata));
        Assert.Equal(id, ScanEventRouting.GetMetadataGuid(metadata, ScanEventRouting.TemplateIdMetadata));
        Assert.Null(ScanEventRouting.GetMetadata(null, "x"));
        Assert.Null(ScanEventRouting.GetMetadataGuid(metadata, "missing"));
    }
}

public class MessagingEventDiscoveryTests
{
    [Fact]
    public void Discover_ShouldIncludeScanRequestedOverride()
    {
        var events = MessagingEventDiscovery.Discover();

        Assert.Contains(events, e => e.EventTypeName.Contains("Scan", StringComparison.OrdinalIgnoreCase));
        Assert.All(events, e => Assert.EndsWith("Event", e.EventTypeName));
    }
}

public class SchemaEventDefinitionProviderTests
{
    [Fact]
    public void GetDefinition_ShouldBindFromRequestConfiguration()
    {
        var requestConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SchemaEvents:Submitted:TopicName"] = "submitted-topic",
            ["SchemaEvents:Submitted:Version"] = "2.0",
            ["SchemaEvents:Submitted:Description"] = "done"
        }).Build();
        var hostConfig = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var requestApp = Substitute.For<IRequestAppConfiguration>();
        requestApp.GetSection("SchemaEvents").Returns(requestConfig.GetSection("SchemaEvents"));

        var provider = new SchemaEventDefinitionProvider(requestApp, hostConfig);

        var def = provider.GetDefinition("Submitted");
        Assert.Equal("submitted-topic", def!.TopicName);
        Assert.Equal("2.0", def.Version);
        Assert.Null(provider.GetDefinition(" "));
        Assert.Null(provider.GetDefinition("Missing"));
    }
}
