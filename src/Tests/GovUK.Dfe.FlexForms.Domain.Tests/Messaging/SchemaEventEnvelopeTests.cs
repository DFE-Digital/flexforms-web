using GovUK.Dfe.FlexForms.Domain.Models.Messaging;

namespace GovUK.Dfe.FlexForms.Domain.Tests.Messaging;

public class SchemaEventEnvelopeTests
{
    [Fact]
    public void Can_create_envelope_with_required_and_optional_metadata()
    {
        var payload = new Dictionary<string, object?> { ["applicationId"] = Guid.NewGuid().ToString() };
        var metadata = new Dictionary<string, object?> { ["tenantId"] = "tenant-1" };

        var envelope = new SchemaEventEnvelope
        {
            MessageType = "TransferSubmitted",
            Version = "1.0",
            TopicName = "transfer-submitted",
            Payload = payload,
            Metadata = metadata
        };

        Assert.Equal("TransferSubmitted", envelope.MessageType);
        Assert.Equal("1.0", envelope.Version);
        Assert.Equal("transfer-submitted", envelope.TopicName);
        Assert.Same(payload, envelope.Payload);
        Assert.Same(metadata, envelope.Metadata);
    }
}
