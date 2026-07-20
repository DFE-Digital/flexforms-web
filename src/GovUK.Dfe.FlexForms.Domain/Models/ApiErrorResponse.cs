using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace GovUK.Dfe.FlexForms.Domain.Models;

[ExcludeFromCodeCoverage]
public class ApiErrorResponse
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("errors")]
    public Dictionary<string, string[]>? Errors { get; set; }

    public bool HasValidationErrors => Errors?.Any() == true;
}

[ExcludeFromCodeCoverage]
public class ApiErrorParsingResult
{
    public bool IsSuccess { get; init; }
    public ApiErrorResponse? ErrorResponse { get; init; }
    public string? RawError { get; init; }

    public static ApiErrorParsingResult Success(ApiErrorResponse errorResponse) => new()
    {
        IsSuccess = true,
        ErrorResponse = errorResponse
    };

    public static ApiErrorParsingResult Failure(string rawError) => new()
    {
        IsSuccess = false,
        RawError = rawError
    };
} 