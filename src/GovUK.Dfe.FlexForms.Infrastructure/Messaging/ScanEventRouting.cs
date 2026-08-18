using System.Text.Json;
using GovUK.Dfe.CoreLibs.Messaging.Contracts.Messages.Events;
using MassTransit;

namespace GovUK.Dfe.FlexForms.Infrastructure.Messaging;

/// <summary>
/// Header and metadata keys used to route scan messages in code (tenant → template → user).
/// Must stay aligned with API <c>ScanEventRouting</c> and the file scanner.
/// </summary>
public static class ScanEventRouting
{
    public const string TenantIdHeader = "TenantId";
    public const string TenantNameHeader = "TenantName";
    public const string TemplateIdHeader = "TemplateId";
    public const string UserIdHeader = "UserId";

    public const string TenantIdMetadata = "TenantId";
    public const string TenantNameMetadata = "TenantName";
    public const string ApplicationNameMetadata = "ApplicationName";
    public const string TemplateIdMetadata = "templateId";
    public const string UserIdMetadata = "userId";
    public const string ApplicationIdMetadata = "applicationId";
    public const string ReferenceMetadata = "Reference";
    public const string OriginalFileNameMetadata = "originalFileName";
    public const string InstanceIdentifierMetadata = "InstanceIdentifier";

    public static string? GetHeader(Headers headers, string key)
    {
        var value = headers.Get<string>(key);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static string? GetMetadata(IDictionary<string, object>? metadata, string key)
    {
        if (metadata is null)
            return null;

        foreach (var pair in metadata)
        {
            if (!string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                continue;

            var text = pair.Value switch
            {
                null => null,
                JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString(),
                JsonElement element => element.ToString(),
                _ => pair.Value.ToString()
            };

            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        return null;
    }

    public static Guid? GetMetadataGuid(IDictionary<string, object>? metadata, string key)
    {
        var text = GetMetadata(metadata, key);
        return Guid.TryParse(text, out var id) ? id : null;
    }

    public static string? ResolveTenantId(Headers headers, IDictionary<string, object>? metadata)
    {
        var fromHeader = GetHeader(headers, TenantIdHeader);
        return fromHeader ?? GetMetadata(metadata, TenantIdMetadata);
    }

    public static string? ResolveTenantName(Headers headers, IDictionary<string, object>? metadata) =>
        GetHeader(headers, TenantNameHeader)
        ?? GetMetadata(metadata, TenantNameMetadata)
        ?? GetMetadata(metadata, ApplicationNameMetadata);

    public static Guid? ResolveTemplateId(Headers headers, IDictionary<string, object>? metadata)
    {
        var text = GetHeader(headers, TemplateIdHeader) ?? GetMetadata(metadata, TemplateIdMetadata);
        return Guid.TryParse(text, out var id) ? id : null;
    }

    public static Guid? ResolveUserId(Headers headers, IDictionary<string, object>? metadata)
    {
        var text = GetHeader(headers, UserIdHeader) ?? GetMetadata(metadata, UserIdMetadata);
        return Guid.TryParse(text, out var id) ? id : null;
    }
}
