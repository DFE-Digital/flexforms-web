namespace GovUK.Dfe.FlexForms.Web.Services.Platform;

/// <summary>
/// Acquires Entra app-only access tokens for platform API endpoints.
/// </summary>
public interface IPlatformAccessTokenProvider
{
    /// <summary>
    /// Returns a bearer access token scoped for platform host/tenant configuration endpoints.
    /// </summary>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
