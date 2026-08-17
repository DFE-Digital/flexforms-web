namespace GovUK.Dfe.FlexForms.Domain.FormEngine;

public readonly record struct CollectionFlowRoute(string FlowId, string InstanceId, string PageId);

public readonly record struct DerivedFlowRoute(string FlowId, string ItemId, string PageId);

/// <summary>
/// Parses form-engine page-id routes. Keep the path shapes stable; in-flight URLs depend on them.
/// </summary>
public static class FormRouteParser
{
    public const string CollectionFlowSegment = "flow";
    public const string DerivedFlowSegment = "derived";

    /// <summary>
    /// Collection flow: <c>flow/{flowId}/{instanceId}/{pageId?}</c>
    /// </summary>
    public static bool TryParseCollectionFlow(string? pageId, out CollectionFlowRoute route)
    {
        route = default;
        if (string.IsNullOrEmpty(pageId))
            return false;

        var parts = pageId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 || !parts[0].Equals(CollectionFlowSegment, StringComparison.OrdinalIgnoreCase))
            return false;

        route = new CollectionFlowRoute(
            parts[1],
            parts[2],
            parts.Length > 3 ? parts[3] : string.Empty);
        return true;
    }

    /// <summary>
    /// Derived collection flow: <c>{flowId}/derived/{itemId}/{pageId?}</c>
    /// </summary>
    public static bool TryParseDerivedFlow(string? pageId, out DerivedFlowRoute route)
    {
        route = default;
        if (string.IsNullOrEmpty(pageId))
            return false;

        var parts = pageId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 || !parts[1].Equals(DerivedFlowSegment, StringComparison.OrdinalIgnoreCase))
            return false;

        route = new DerivedFlowRoute(
            parts[0],
            parts[2],
            parts.Length > 3 ? parts[3] : string.Empty);
        return true;
    }

    public static bool LooksLikeCollectionFlow(string? pageId) =>
        !string.IsNullOrEmpty(pageId)
        && pageId.StartsWith($"{CollectionFlowSegment}/", StringComparison.OrdinalIgnoreCase);

    public static bool IsCollectionFlow(string? pageId) => TryParseCollectionFlow(pageId, out _);

    public static bool IsDerivedFlow(string? pageId) => TryParseDerivedFlow(pageId, out _);

    /// <summary>
    /// Navigation-history scope: <c>{reference}:{task}</c> or <c>{reference}:{task}:flow:{flowId}:{instanceId}</c>.
    /// </summary>
    public static string HistoryScope(string referenceNumber, string taskId, string? pageId)
    {
        if (TryParseCollectionFlow(pageId, out var route))
            return $"{referenceNumber}:{taskId}:flow:{route.FlowId}:{route.InstanceId}";

        return $"{referenceNumber}:{taskId}";
    }
}
