using System.Text.Json.Serialization;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Api;

[JsonConverter(typeof(JsonStringEnumConverter<ApplicationStatus>))]
public enum ApplicationStatus
{
    Created,
    InProgress,
    Submitted,
    Deleted,
}

public sealed record CreateApplicationRequest(string TemplateId, string? InitialResponseBody);

public sealed record CreateApplicationResponse(string ApplicationId, string ApplicationReference, string? Status);

public sealed record AddApplicationResponseRequest(string ResponseBody);

public sealed record AddContributorRequest(string Email, string Name);

public sealed record CustomApplicationStatusRequest(ApplicationStatus ApplicationStatus, string Label);

public sealed record UploadDto(string Id, string ApplicationId);

public sealed record ExchangeTokenRequest([property: JsonPropertyName("accessToken")] string AccessToken);

public sealed record ExchangeTokenResponse(
    [property: JsonPropertyName("access_token")] string? AccessToken,
    [property: JsonPropertyName("token_type")] string? TokenType,
    [property: JsonPropertyName("expires_in")] int? ExpiresIn);

public sealed record CustomApplicationStatus(
    string CustomApplicationStatusId,
    string TemplateId,
    ApplicationStatus ApplicationStatus,
    string Label,
    string CreatedOn,
    string CreatedBy);

public sealed record FileValidationRequest(bool IsValid, string Message, string? CorrelationId, string Source);

public sealed record FileValidationResult(string Id, string ApplicationId, string Name);

public sealed record TemplateDto(
    string TemplateId,
    string? Name,
    DateTimeOffset CreatedOn,
    string? LatestVersionNumber,
    bool IsLive);

public sealed record TenantUserDto(string UserId, string? Email, string? Name);

public sealed record TenantUsersResult(IReadOnlyList<TenantUserDto>? Items);

public sealed record TenantRoleDto(string RoleId, string Name, bool IsSystem);


public sealed record CreateUserRoleRequest(string Email, string Name, string Role, string[] TemplateIds);

public sealed record UserDto(
    string UserId,
    string Name,
    string Email,
    string RoleId,
    CreateUserRoleAuthorization Authorization);

public sealed record CreateUserRoleAuthorization(
    IReadOnlyList<CreateUserRolePermission> Permissions,
    IReadOnlyList<string> Roles);

public sealed record CreateUserRolePermission(
    string ApplicationId,
    string ResourceKey,
    string ResourceType,
    string AccessType);
