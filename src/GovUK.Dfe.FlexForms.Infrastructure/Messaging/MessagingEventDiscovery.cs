using System.Reflection;
using GovUK.Dfe.CoreLibs.Messaging.Contracts.Entities.Topics;
using GovUK.Dfe.CoreLibs.Messaging.Contracts.Messages.Events;

namespace GovUK.Dfe.FlexForms.Infrastructure.Messaging;

/// <summary>
/// Discovers publishable/consumable message types from CoreLibs Messaging.Contracts
/// and resolves Azure Service Bus topic names via <see cref="TopicNames"/> convention.
/// </summary>
public static class MessagingEventDiscovery
{
    private static readonly Lazy<IReadOnlyList<DiscoveredMessagingEvent>> Cached =
        new(DiscoverCore, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Known mismatches between type name (minus "Event") and <see cref="TopicNames"/> constant names.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> TopicOverrides =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(ScanRequestedEvent)] = TopicNames.ScanRequests
        };

    /// <summary>
    /// Returns all discovered messaging event types (cached for process lifetime).
    /// </summary>
    public static IReadOnlyList<DiscoveredMessagingEvent> Discover() => Cached.Value;

    private static IReadOnlyList<DiscoveredMessagingEvent> DiscoverCore()
    {
        var contractsAssembly = typeof(TransferApplicationSubmittedEvent).Assembly;
        var eventsNamespace = typeof(TransferApplicationSubmittedEvent).Namespace
            ?? "GovUK.Dfe.CoreLibs.Messaging.Contracts.Messages.Events";

        var topicByName = typeof(TopicNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .ToDictionary(
                f => f.Name,
                f => (string)f.GetRawConstantValue()!,
                StringComparer.OrdinalIgnoreCase);

        var results = new List<DiscoveredMessagingEvent>();

        foreach (var type in contractsAssembly.GetExportedTypes())
        {
            if (type.Namespace is null
                || !string.Equals(type.Namespace, eventsNamespace, StringComparison.Ordinal)
                || type.IsAbstract
                || type.IsInterface
                || !type.IsClass && !type.IsValueType)
            {
                continue;
            }

            // Convention: public CLR message types live in Messages.Events and end with "Event".
            if (!type.Name.EndsWith("Event", StringComparison.Ordinal))
                continue;

            var topicName = ResolveTopicName(type.Name, topicByName);
            results.Add(new DiscoveredMessagingEvent(type, type.Name, topicName));
        }

        return results
            .OrderBy(e => e.EventTypeName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? ResolveTopicName(
        string eventTypeName,
        IReadOnlyDictionary<string, string> topicByName)
    {
        if (TopicOverrides.TryGetValue(eventTypeName, out var overridden))
            return overridden;

        var withoutSuffix = eventTypeName.EndsWith("Event", StringComparison.Ordinal)
            ? eventTypeName[..^"Event".Length]
            : eventTypeName;

        if (topicByName.TryGetValue(withoutSuffix, out var exact))
            return exact;

        // ScanRequest → ScanRequests style pluralisation
        if (topicByName.TryGetValue(withoutSuffix + "s", out var plural))
            return plural;

        return null;
    }
}
