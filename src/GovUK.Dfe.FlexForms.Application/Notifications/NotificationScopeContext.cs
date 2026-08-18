namespace GovUK.Dfe.FlexForms.Application.Notifications;

/// <summary>
/// Builds scoped notification contexts for tenant list/badge queries.
/// Stored values look like "{ApplicationName}|file-upload|{fileId}".
/// </summary>
public static class NotificationScopeContext
{
    public static string PrefixDetail(string applicationContext, string? detailContext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationContext);

        return string.IsNullOrWhiteSpace(detailContext)
            ? applicationContext.Trim()
            : $"{applicationContext.Trim()}|{detailContext.Trim()}";
    }

    public static string Build(string applicationContext, params string?[] parts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationContext);

        var segments = new List<string> { applicationContext.Trim() };
        foreach (var part in parts)
        {
            if (!string.IsNullOrWhiteSpace(part))
                segments.Add(part.Trim());
        }

        return string.Join('|', segments);
    }
}
