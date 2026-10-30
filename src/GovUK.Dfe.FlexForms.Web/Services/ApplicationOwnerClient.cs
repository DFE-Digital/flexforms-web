using System.Net.Http.Json;
using System.Text.Json;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;

namespace GovUK.Dfe.FlexForms.Web.Services;

public interface IApplicationOwnerClient
{
    Task<ApplicationOwnerChangeResult> ChangeOwnerAsync(
        Guid applicationId,
        string email,
        CancellationToken cancellationToken = default);
}

public sealed class ApplicationOwnerChangeResult
{
    public Guid ApplicationId { get; set; }

    public bool TemplateReadGranted { get; set; }
}

/// <summary>
/// Calls the owner-change endpoint. The published API client does not include this method yet.
/// </summary>
public sealed class ApplicationOwnerClient(HttpClient httpClient, string baseUrl) : IApplicationOwnerClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ApplicationOwnerChangeResult> ChangeOwnerAsync(
        Guid applicationId,
        string email,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, OwnerUrl(applicationId))
        {
            Content = JsonContent.Create(new { email })
        };
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new ExternalApplicationsException(
                $"The HTTP status code of the response was not expected ({(int)response.StatusCode}).",
                (int)response.StatusCode,
                body,
                new Dictionary<string, IEnumerable<string>>(),
                null!);
        }

        return JsonSerializer.Deserialize<ApplicationOwnerChangeResult>(body, JsonOptions)
               ?? new ApplicationOwnerChangeResult { ApplicationId = applicationId };
    }

    private string OwnerUrl(Guid applicationId)
    {
        var root = string.IsNullOrWhiteSpace(baseUrl) ? string.Empty : baseUrl.TrimEnd('/') + "/";
        return $"{root}v1/Applications/{applicationId}/owner";
    }
}
