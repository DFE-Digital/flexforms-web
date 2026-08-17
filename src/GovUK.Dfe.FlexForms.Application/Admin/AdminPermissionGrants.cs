using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Encoding, parsing, and validation for Admin permission grant keys
/// (<c>{ResourceType}|{ResourceKey}|{AccessType}</c>).
/// </summary>
public static class AdminPermissionGrants
{
    public const string AnyResourceKey = "Any";

    public static string EncodeGrantKey(ResourceType resourceType, string resourceKey, AccessType accessType) =>
        $"{resourceType}|{resourceKey.Trim()}|{accessType}";

    public static string FormatGrant(string key)
    {
        var parsed = ParseGrantKey(key);
        return parsed is null
            ? key
            : $"{parsed.Value.ResourceType} / {parsed.Value.ResourceKey} / {parsed.Value.AccessType}";
    }

    public static List<string> NormalizeGrants(IEnumerable<string>? grants) =>
        (grants ?? [])
            .Select(ParseGrantKey)
            .Where(g => g is not null)
            .Select(g => EncodeGrantKey(g!.Value.ResourceType, g.Value.ResourceKey, g.Value.AccessType))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static (ResourceType ResourceType, string ResourceKey, AccessType AccessType)? ParseGrantKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var parts = key.Split('|', 3);
        if (parts.Length != 3)
            return null;

        if (!Enum.TryParse<ResourceType>(parts[0], ignoreCase: true, out var resourceType))
            return null;

        if (string.IsNullOrWhiteSpace(parts[1]))
            return null;

        if (!Enum.TryParse<AccessType>(parts[2], ignoreCase: true, out var accessType))
            return null;

        return (resourceType, parts[1].Trim(), accessType);
    }

    /// <summary>
    /// Mirrors API <c>RolePermissionGrantRules</c>.
    /// </summary>
    public static string? ValidateGrant(ResourceType resourceType, string resourceKey, AccessType accessType)
    {
        var key = resourceKey.Trim();
        if (accessType == AccessType.Manage)
        {
            if (resourceType != ResourceType.Template && resourceType != ResourceType.User)
            {
                return "Access type 'Manage' is only allowed for Template or User permissions.";
            }

            if (string.Equals(key, AnyResourceKey, StringComparison.OrdinalIgnoreCase))
                return null;
        }
        else if (string.Equals(key, AnyResourceKey, StringComparison.OrdinalIgnoreCase))
        {
            if ((resourceType == ResourceType.Template && accessType == AccessType.Write)
                || (resourceType == ResourceType.Template && accessType == AccessType.Manage)
                || (resourceType == ResourceType.User && accessType == AccessType.Manage)
                || (resourceType == ResourceType.Application && accessType == AccessType.Read)
                || (resourceType == ResourceType.ApplicationFiles && accessType == AccessType.Read)
                || (resourceType == ResourceType.FileValidation && accessType == AccessType.Write))
            {
                return null;
            }

            return $"Resource key '{AnyResourceKey}' is only allowed for Template — Write, " +
                   "Template — Manage, User — Manage, Application — Read, " +
                   "ApplicationFiles — Read, or FileValidation — Write. " +
                   "For other combinations, use a specific resource id or email.";
        }

        return resourceType switch
        {
            ResourceType.Application or ResourceType.ApplicationFiles or ResourceType.Template
                or ResourceType.File or ResourceType.FileValidation or ResourceType.Task or ResourceType.TaskGroup
                or ResourceType.Page or ResourceType.Field
                when !Guid.TryParse(key, out var id) || id == Guid.Empty
                => $"{resourceType} resource key must be a valid non-empty GUID (the resource id) or 'Any' (where allowed).",

            ResourceType.User or ResourceType.Notifications
                when !key.Contains('@', StringComparison.Ordinal) && !Guid.TryParse(key, out _)
                => $"{resourceType} resource key must be a user email (or a service client id).",

            _ => null
        };
    }

    /// <summary>
    /// Same shape rules as role grants, but Manage is never allowed on an individual user.
    /// </summary>
    public static string? ValidateUserGrant(ResourceType resourceType, string resourceKey, AccessType accessType)
    {
        if (accessType == AccessType.Manage)
        {
            return "Access type 'Manage' cannot be granted to an individual user. " +
                   "Assign Manage via a tenant role instead.";
        }

        return ValidateGrant(resourceType, resourceKey, accessType);
    }

    public static List<RolePermissionGrantDto> ToGrantDtos(IEnumerable<string> grants) =>
        grants
            .Select(ParseGrantKey)
            .Where(g => g is not null)
            .Select(g => g.GetValueOrDefault())
            .Select(g => new RolePermissionGrantDto
            {
                ResourceType = g.ResourceType,
                ResourceKey = g.ResourceKey,
                AccessType = g.AccessType
            })
            .ToList();
}
