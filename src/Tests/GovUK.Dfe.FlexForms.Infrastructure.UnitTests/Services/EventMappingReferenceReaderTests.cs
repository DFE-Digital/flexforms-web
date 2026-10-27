using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Services;

public class EventMappingReferenceReaderTests
{
    private static EventMappingReferenceReader ReaderFor(Dictionary<string, string?> settings)
    {
        var requestConfig = Substitute.For<IRequestAppConfiguration>();
        requestConfig.Current.Returns(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        return new EventMappingReferenceReader(requestConfig);
    }

    [Fact]
    public void GetFieldReferences_ShouldFindEveryAnswerAMappingReads_UnderEitherTemplateKey()
    {
        var reader = ReaderFor(new Dictionary<string, string?>
        {
            ["EventMappings:BasePath"] = "EventMappings",
            ["EventMappings:9a1f:TransferSubmittedEvent:mappingId"] = "transfer-v1",
            ["EventMappings:9a1f:TransferSubmittedEvent:fieldMappings:TrustName:sourceFieldId"] = "trustName",
            ["EventMappings:9a1f:TransferSubmittedEvent:fieldMappings:Address:sourceFieldIds:0"] = "street",
            ["EventMappings:9a1f:TransferSubmittedEvent:fieldMappings:Address:sourceFieldIds:1"] = "town",
            ["EventMappings:9a1f:TransferSubmittedEvent:fieldMappings:Academies:collectionMapping:sourceCollectionFieldId"] = "academies",
            ["EventMappings:9a1f:TransferSubmittedEvent:fieldMappings:Academies:collectionMapping:itemMappings:Urn:sourceFieldId"] = "academySearch",
            ["EventMappings:form-001:ApplicationSubmittedEvent:mappingId"] = "submitted-v1",
            ["EventMappings:form-001:ApplicationSubmittedEvent:fieldMappings:Reference:sourceFieldId"] = "trustName",
            ["EventMappings:other-template:ApplicationSubmittedEvent:fieldMappings:Reference:sourceFieldId"] = "elsewhere"
        });

        var references = reader.GetFieldReferences(["9A1F", "form-001", ""]);

        Assert.Equal(6, references.Count);
        Assert.Equivalent(
            new[]
            {
                new EventMappingFieldReference("trustName", "TransferSubmittedEvent", "transfer-v1"),
                new EventMappingFieldReference("street", "TransferSubmittedEvent", "transfer-v1"),
                new EventMappingFieldReference("town", "TransferSubmittedEvent", "transfer-v1"),
                new EventMappingFieldReference("academies", "TransferSubmittedEvent", "transfer-v1"),
                new EventMappingFieldReference("academySearch", "TransferSubmittedEvent", "transfer-v1"),
                new EventMappingFieldReference("trustName", "ApplicationSubmittedEvent", "submitted-v1")
            },
            references,
            strict: true);
    }

    [Fact]
    public void GetFieldReferences_ShouldReturnNothing_WhenTheTenantHasNoMappings()
    {
        var reader = ReaderFor([]);

        Assert.Empty(reader.GetFieldReferences(["9a1f"]));
    }
}
