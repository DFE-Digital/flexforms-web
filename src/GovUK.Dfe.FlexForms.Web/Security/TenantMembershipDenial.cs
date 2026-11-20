using GovUK.Dfe.CoreLibs.Http.Models;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Recognises "signed in, but not allowed into this tenant" denials from token exchange.
/// Api.Client <c>TokenExchangeHandler</c> turns the API's exchange 403 (no membership) or 404
/// (user not found) into a synthetic 403 carrying <see cref="ErrorId"/>. This is authorisation,
/// not an expired session, so Web shows the reason instead of signing the user out.
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
    private const string NotMemberMarker = "not a member of tenant";

    /// <summary>
    /// Phrase in the API's exchange 404 ("User not found for email …"). Test Auth can still send
    /// an OTP for an email that has no EA user row.
    /// </summary>
    private const string UserNotFoundMarker = "user not found";

    public static bool IsMatch(ExternalApplicationsException exception)
    {
        if (exception is ExternalApplicationsException<ExceptionResponse> { Result: { } typed }
            && IsMatch(typed))
        {
            return true;
        }

        // Synthetic client 403 (preferred path after Api.Client bump).
        if (exception.StatusCode == StatusCodes.Status403Forbidden
            && (ContainsErrorId(exception.Response) || ContainsNotMemberMarker(exception.Response)))
        {
            return true;
        }

        // Raw API 404 "User not found" if it reaches Web before the client maps it.
        return exception.StatusCode == StatusCodes.Status404NotFound
            && ContainsUserNotFoundMarker(exception.Response);
    }

    public static bool IsMatch(ExceptionResponse? response) =>
        response is not null
        && (IsMatch(response.ErrorId)
            || ContainsNotMemberMarker(response.Message)
            || ContainsUserNotFoundMarker(response.Message));

    private static bool IsMatch(string? errorId) =>
        string.Equals(errorId, ErrorId, StringComparison.Ordinal);

    private static bool ContainsErrorId(string? text) =>
        text?.Contains(ErrorId, StringComparison.Ordinal) == true;

    private static bool ContainsNotMemberMarker(string? text) =>
        text?.Contains(NotMemberMarker, StringComparison.OrdinalIgnoreCase) == true;

    private static bool ContainsUserNotFoundMarker(string? text) =>
        text?.Contains(UserNotFoundMarker, StringComparison.OrdinalIgnoreCase) == true;
}
