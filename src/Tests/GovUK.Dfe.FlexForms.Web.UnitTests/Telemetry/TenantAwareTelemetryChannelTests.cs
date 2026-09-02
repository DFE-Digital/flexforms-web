using GovUK.Dfe.FlexForms.Web.Telemetry;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Telemetry;

public class TenantAwareTelemetryChannelTests
{
    [Fact]
    public void Send_ShouldUseHostChannel_WhenNoTenantScope()
    {
        var host = new RecordingChannel();
        var tenant = new RecordingChannel();
        var channel = new TenantAwareTelemetryChannel(cs =>
            cs.Contains("11111111-1111-1111-1111-111111111111", StringComparison.OrdinalIgnoreCase) ? tenant : host);
        channel.Initialize(new TelemetryConfiguration
        {
            ConnectionString = "InstrumentationKey=host-key"
        });

        channel.Send(new RequestTelemetry { Name = "host" });

        Assert.Single(host.Items);
        Assert.Empty(tenant.Items);
    }

    [Fact]
    public void Send_ShouldRouteToTenantChannel_AndStampInstrumentationKey()
    {
        var host = new RecordingChannel();
        var tenant = new RecordingChannel();
        var channel = new TenantAwareTelemetryChannel(cs =>
            cs.Contains("11111111-1111-1111-1111-111111111111", StringComparison.OrdinalIgnoreCase) ? tenant : host);
        channel.Initialize(new TelemetryConfiguration
        {
            ConnectionString = "InstrumentationKey=host-key"
        });

        var tenantCs = "InstrumentationKey=11111111-1111-1111-1111-111111111111;IngestionEndpoint=https://uksouth-1.in.applicationinsights.azure.com/";
        using (TenantApplicationInsightsScope.Begin(tenantCs))
        {
            channel.Send(new RequestTelemetry { Name = "tenant" });
        }

        Assert.Empty(host.Items);
        Assert.Single(tenant.Items);
        Assert.Equal("11111111-1111-1111-1111-111111111111", tenant.Items[0].Context.InstrumentationKey);
    }

    [Fact]
    public void FromConfiguration_ShouldReadTenantConnectionString()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApplicationInsights:ConnectionString"] = " InstrumentationKey=abc "
            })
            .Build();

        Assert.Equal(
            "InstrumentationKey=abc",
            TenantApplicationInsightsConnection.FromConfiguration(configuration));
    }

    private sealed class RecordingChannel : ITelemetryChannel
    {
        public List<ITelemetry> Items { get; } = [];
        public bool? DeveloperMode { get; set; }
        public string? EndpointAddress { get; set; }
        public void Send(ITelemetry item) => Items.Add(item);
        public void Flush() { }
        public void Dispose() { }
    }
}
