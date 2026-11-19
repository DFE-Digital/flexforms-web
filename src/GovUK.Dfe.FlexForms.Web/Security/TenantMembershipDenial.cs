using GovUK.Dfe.CoreLibs.Http.Models;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Recognises the API client's "signed in, but not a member of this tenant" denial.
/// Api.Client <c>TokenExchangeHandler</c> turns the API's exchange 403 into a synthetic 403
/// carrying <see cref="ErrorId"/>. This is authorisation, not an expired session, so Web shows
/// the reason instead of signing the user out as a token failure.
/// </summary>
public static class TenantMembershipDenial
{
    /// <summary>
    /// Mirrors Api.Client <c>TokenExchangeErrors.NoTenantMembershipErrorId</c>. Kept as a string here
    /// so Web compiles against older client packages; the two values must stay identical.
    /// </summary>
    public const string ErrorId = "Exchange-403-NoTenantMembership";

    /// <summary>
    /// Phrase in the API's own exchange 403 ("User is not a member of tenant '…'"), in case the
    /// raw API response reaches Web.
    /// </summary>
    private const string ApiMessageMarker = "not a member of tenant";

    public static bool IsMatch(ExternalApplicationsException exception)
    {
        if (exception.StatusCode != StatusCodes.Status403Forbidden)
            return false;

        if (exception is ExternalApplicationsException<ExceptionResponse> { Result: { } typed }
            && (IsMatch(typed.ErrorId) || ContainsMarker(typed.Message)))
        {
            return true;
        }

        // The generated client throws an untyped exception for 403, with the body in Response.
        return ContainsErrorId(exception.Response) || ContainsMarker(exception.Response);
    }

    public static bool IsMatch(ExceptionResponse? response) =>
        response is not null
        && (IsMatch(response.ErrorId) || ContainsMarker(response.Message));

    private static bool IsMatch(string? errorId) =>
        string.Equals(errorId, ErrorId, StringComparison.Ordinal);

    private static bool ContainsErrorId(string? text) =>
        text?.Contains(ErrorId, StringComparison.Ordinal) == true;

    private static bool ContainsMarker(string? text) =>
        text?.Contains(ApiMessageMarker, StringComparison.OrdinalIgnoreCase) == true;
}
