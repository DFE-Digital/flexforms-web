using System.Collections.Concurrent;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
using Microsoft.AspNetCore.Http;

namespace GovUK.Dfe.FlexForms.Web.Telemetry;

/// <summary>
/// Routes telemetry to the tenant Application Insights resource when
/// <see cref="TenantApplicationInsightsScope"/> or the current HTTP request has a tenant
/// connection string; otherwise uses the host resource.
/// </summary>
public sealed class TenantAwareTelemetryChannel : ITelemetryChannel, ITelemetryModule
{
    private readonly ConcurrentDictionary<string, ITelemetryChannel> _tenantChannels =
        new(StringComparer.Ordinal);
    private readonly Func<string, ITelemetryChannel> _createChannel;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private ITelemetryChannel? _hostChannel;

    public TenantAwareTelemetryChannel(IHttpContextAccessor httpContextAccessor)
        : this(CreateServerChannel, httpContextAccessor)
    {
    }

    internal TenantAwareTelemetryChannel(
        Func<string, ITelemetryChannel> createChannel,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _createChannel = createChannel;
        _httpContextAccessor = httpContextAccessor;
    }

    public bool? DeveloperMode { get; set; }

    public string? EndpointAddress { get; set; }

    public void Initialize(TelemetryConfiguration configuration)
    {
        var hostConnectionString = configuration.ConnectionString ?? string.Empty;
        var inner = _createChannel(hostConnectionString);
        if (inner is ITelemetryModule module)
        {
            module.Initialize(configuration);
        }

        if (DeveloperMode is not null && inner is ServerTelemetryChannel server)
        {
            server.DeveloperMode = DeveloperMode;
        }

        _hostChannel = inner;
    }

    public void Send(ITelemetry item)
    {
        var connectionString = TenantApplicationInsightsScope.CurrentConnectionString
            ?? TenantApplicationInsightsConnection.FromHttpContext(_httpContextAccessor?.HttpContext);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _hostChannel?.Send(item);
            return;
        }

        if (TenantApplicationInsightsConnection.TryGetInstrumentationKey(connectionString, out var instrumentationKey))
        {
            item.Context.InstrumentationKey = instrumentationKey;
        }

        GetOrCreateTenantChannel(connectionString).Send(item);
    }

    public void Flush()
    {
        _hostChannel?.Flush();
        foreach (var channel in _tenantChannels.Values)
        {
            channel.Flush();
        }
    }

    public void Dispose()
    {
        _hostChannel?.Dispose();
        foreach (var channel in _tenantChannels.Values)
        {
            channel.Dispose();
        }

        _tenantChannels.Clear();
    }

    private ITelemetryChannel GetOrCreateTenantChannel(string connectionString) =>
        _tenantChannels.GetOrAdd(connectionString, cs =>
        {
            var channel = _createChannel(cs);
            if (channel is ITelemetryModule module)
            {
                var config = new TelemetryConfiguration { ConnectionString = cs, TelemetryChannel = channel };
                module.Initialize(config);
            }

            return channel;
        });

    private static ITelemetryChannel CreateServerChannel(string _) => new ServerTelemetryChannel();
}
