using GovUK.Dfe.CoreLibs.Messaging.Contracts.Messages.Events;
using GovUK.Dfe.FlexForms.Infrastructure.Messaging;
using GovUK.Dfe.FlexForms.Web.Configuration;
using GovUK.Dfe.FlexForms.Web.Services.Tenant;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using GovUK.Dfe.FlexForms.Web.Telemetry;
using MassTransit;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Web.Messaging;

/// <summary>
/// Binds tenant context for scan-result messages from headers/metadata (same keys as publish),
/// then exposes it via <see cref="ITenantRequestContext"/> and <see cref="AmbientTenantRequestContext"/>
/// so S2S API calls include <c>X-Tenant-ID</c>. Must be an open generic so MassTransit can
/// register it with <c>UseConsumeFilter(typeof(ScanResultTenantContextFilter&lt;&gt;), ...)</c>.
/// </summary>
public sealed class ScanResultTenantContextFilter<T>(
    ITenantRequestContext tenantRequestContext,
    IOptions<PlatformBootstrapOptions> bootstrapOptions,
    ILogger<ScanResultTenantContextFilter<T>> logger,
    TenantConfigurationLoader? tenantConfigurationLoader = null) : IFilter<ConsumeContext<T>>
    where T : class
{
    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        var metadata = context.Message is ScanResultEvent scanResult ? scanResult.Metadata : null;
        var fileId = context.Message is ScanResultEvent scan ? scan.FileId : typeof(T).Name;

        var tenantIdValue = ScanEventRouting.ResolveTenantId(context.Headers, metadata);
        if (!Guid.TryParse(tenantIdValue, out var tenantId))
        {
            logger.LogWarning(
                "Scan result {FileId} has no TenantId header or metadata; skipping",
                fileId);
            return;
        }

        tenantRequestContext.TenantId = tenantId;
        tenantRequestContext.TenantName = ScanEventRouting.ResolveTenantName(context.Headers, metadata);

        if (bootstrapOptions.Value.Enabled && tenantConfigurationLoader is not null)
        {
            try
            {
                var tenantConfig = await tenantConfigurationLoader.LoadAsync(tenantId, context.CancellationToken);
                tenantRequestContext.TenantId = tenantConfig.TenantId;
                tenantRequestContext.TenantName = tenantConfig.TenantName;
                tenantRequestContext.TenantConfiguration = new ConfigurationBuilder()
                    .AddInMemoryCollection(tenantConfig.Configuration.ToList())
                    .Build();
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to load tenant configuration for scan result {FileId} (TenantId {TenantId})",
                    fileId,
                    tenantId);
                return;
            }
        }

        using (AmbientTenantRequestContext.Use(tenantRequestContext))
        using (TenantApplicationInsightsScope.Begin(
                   TenantApplicationInsightsConnection.FromConfiguration(tenantRequestContext.TenantConfiguration)))
        {
            await next.Send(context);
        }
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("scanResultTenantContext");
}
