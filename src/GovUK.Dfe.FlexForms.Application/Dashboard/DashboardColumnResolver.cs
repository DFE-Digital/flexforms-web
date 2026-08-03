using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Application.Dashboard;

/// <summary>
/// Resolved dashboard column ready for rendering.
/// </summary>
public sealed record DashboardColumn(
    string Key,
    string Header,
    DashboardColumnKind Kind,
    string? FieldId,
    int Order);

public enum DashboardColumnKind
{
    System,
    Field
}

/// <summary>
/// Builds the ordered dashboard column list from the latest template (Policy 1).
/// </summary>
public static class DashboardColumnResolver
{
    public const string SystemReference = "reference";
    public const string SystemDateStarted = "dateStarted";
    public const string SystemDateSubmitted = "dateSubmitted";
    public const string SystemStatus = "status";
    public const string SystemAction = "action";

    private static readonly IReadOnlyDictionary<string, string> DefaultSystemHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [SystemReference] = "Reference number",
            [SystemDateStarted] = "Date started",
            [SystemDateSubmitted] = "Date submitted",
            [SystemStatus] = "Status",
            [SystemAction] = "Action"
        };

    /// <summary>
    /// Default system-only layout when the template has no <c>dashboard</c> node.
    /// </summary>
    public static IReadOnlyList<DashboardColumn> DefaultColumns { get; } =
    [
        new(SystemReference, DefaultSystemHeaders[SystemReference], DashboardColumnKind.System, null, 10),
        new(SystemDateStarted, DefaultSystemHeaders[SystemDateStarted], DashboardColumnKind.System, null, 20),
        new(SystemDateSubmitted, DefaultSystemHeaders[SystemDateSubmitted], DashboardColumnKind.System, null, 30),
        new(SystemStatus, DefaultSystemHeaders[SystemStatus], DashboardColumnKind.System, null, 40),
        new(SystemAction, DefaultSystemHeaders[SystemAction], DashboardColumnKind.System, null, 50)
    ];

    public static IReadOnlyList<DashboardColumn> Resolve(FormTemplate? template)
    {
        var configured = template?.Dashboard?.Columns;
        if (configured is null || configured.Count == 0)
            return DefaultColumns;

        var resolved = new List<DashboardColumn>();
        var customFieldCount = 0;

        for (var index = 0; index < configured.Count; index++)
        {
            var definition = configured[index];
            var order = definition.Order ?? ((index + 1) * 10);
            var kind = ResolveKind(definition);

            if (kind == DashboardColumnKind.Field)
            {
                if (string.IsNullOrWhiteSpace(definition.FieldId))
                    continue;

                if (customFieldCount >= DashboardConfiguration.MaxCustomFieldColumns)
                    continue;

                customFieldCount++;
                var header = string.IsNullOrWhiteSpace(definition.Header)
                    ? definition.FieldId.Trim()
                    : definition.Header.Trim();

                resolved.Add(new DashboardColumn(
                    Key: $"field:{definition.FieldId.Trim()}",
                    Header: header,
                    Kind: DashboardColumnKind.Field,
                    FieldId: definition.FieldId.Trim(),
                    Order: order));
                continue;
            }

            var systemId = definition.Id?.Trim();
            if (string.IsNullOrWhiteSpace(systemId)
                || !DefaultSystemHeaders.TryGetValue(systemId, out var defaultHeader))
            {
                continue;
            }

            var systemHeader = string.IsNullOrWhiteSpace(definition.Header)
                ? defaultHeader
                : definition.Header.Trim();

            resolved.Add(new DashboardColumn(
                Key: systemId,
                Header: systemHeader,
                Kind: DashboardColumnKind.System,
                FieldId: null,
                Order: order));
        }

        if (resolved.Count == 0)
            return DefaultColumns;

        // If the author only listed field columns, keep default system columns and merge by order.
        if (resolved.All(c => c.Kind == DashboardColumnKind.Field))
        {
            resolved.AddRange(DefaultColumns);
        }
        else if (!resolved.Any(c => c.Kind == DashboardColumnKind.System && c.Key == SystemAction))
        {
            // Always keep an Action column so rows remain usable.
            var maxOrder = resolved.Max(c => c.Order);
            resolved.Add(new DashboardColumn(
                SystemAction,
                DefaultSystemHeaders[SystemAction],
                DashboardColumnKind.System,
                null,
                maxOrder + 10));
        }

        return resolved
            .OrderBy(c => c.Order)
            .ThenBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static DashboardColumnKind ResolveKind(DashboardColumnDefinition definition)
    {
        if (!string.IsNullOrWhiteSpace(definition.Type))
        {
            if (string.Equals(definition.Type, "system", StringComparison.OrdinalIgnoreCase))
                return DashboardColumnKind.System;

            if (string.Equals(definition.Type, "field", StringComparison.OrdinalIgnoreCase))
                return DashboardColumnKind.Field;
        }

        if (!string.IsNullOrWhiteSpace(definition.FieldId))
            return DashboardColumnKind.Field;

        return DashboardColumnKind.System;
    }
}
