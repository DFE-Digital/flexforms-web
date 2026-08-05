using System.Reflection;
using MassTransit;

namespace GovUK.Dfe.FlexForms.Infrastructure.Messaging;

/// <summary>
/// Wires MassTransit message types to Azure Service Bus topic entity names
/// for all discovered CoreLibs messaging events.
/// </summary>
public static class MessagingEventBusConfigurator
{
    /// <summary>
    /// Calls <c>cfg.Message&lt;T&gt;(m =&gt; m.SetEntityName(topic))</c> for every discovered
    /// event that has a resolved topic name.
    /// </summary>
    public static void ConfigureDiscoveredMessageTopics(IBusFactoryConfigurator cfg)
    {
        ArgumentNullException.ThrowIfNull(cfg);

        foreach (var discovered in MessagingEventDiscovery.Discover())
        {
            if (string.IsNullOrWhiteSpace(discovered.TopicName))
                continue;

            ConfigureMessage(cfg, discovered.ClrType, discovered.TopicName);
        }
    }

    private static void ConfigureMessage(IBusFactoryConfigurator cfg, Type messageType, string topicName)
    {
        var method = typeof(MessagingEventBusConfigurator)
            .GetMethod(nameof(ConfigureMessageCore), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(messageType);

        method.Invoke(null, [cfg, topicName]);
    }

    private static void ConfigureMessageCore<T>(IBusFactoryConfigurator cfg, string topicName)
        where T : class
    {
        cfg.Message<T>(m => m.SetEntityName(topicName));
    }
}
