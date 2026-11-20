using GovUK.Dfe.CoreLibs.Http.Models;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Web.Security;
using System.Text.Json;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Security;

public class TenantMembershipDenialTests
{
    private static readonly Dictionary<string, IEnumerable<string>> NoHeaders = new();

    private static ExternalApplicationsException Untyped(int status, string body) =>
        new("API error", status, body, NoHeaders, null);

    private static ExternalApplicationsException<ExceptionResponse> Typed(ExceptionResponse response) =>
        new(response.Message, response.StatusCode, response.Message, NoHeaders, response, null);

    [Fact]
    public void IsMatch_True_ForClientSynthetic403WithErrorId()
    {
        var body = JsonSerializer.Serialize(new ExceptionResponse
        {
            StatusCode = 403,
            ErrorId = TenantMembershipDenial.ErrorId,
            Message = "You are not authorised to access this service."
        });

        Assert.True(TenantMembershipDenial.IsMatch(Untyped(403, body)));
    }

    [Fact]
    public void IsMatch_True_ForRawApiMembershipMessage()
        => Assert.True(TenantMembershipDenial.IsMatch(Untyped(403, "{\"message\":\"User is not a member of tenant 'Visits'.\"}")));

    [Fact]
    public void IsMatch_True_ForTypedResponseWithErrorId()
        => Assert.True(TenantMembershipDenial.IsMatch(Typed(new ExceptionResponse
        {
            StatusCode = 403,
            ErrorId = TenantMembershipDenial.ErrorId,
            Message = "Denied"
        })));

    [Fact]
    public void IsMatch_False_ForOther403()
        => Assert.False(TenantMembershipDenial.IsMatch(Untyped(403, "{\"message\":\"Role required\"}")));

    [Fact]
    public void IsMatch_False_WhenStatusIsNot403Or404()
    {
        var body = JsonSerializer.Serialize(new ExceptionResponse { ErrorId = TenantMembershipDenial.ErrorId });

        Assert.False(TenantMembershipDenial.IsMatch(Untyped(401, body)));
    }

    [Fact]
    public void IsMatch_True_ForRawApiUserNotFound404()
        => Assert.True(TenantMembershipDenial.IsMatch(
            Untyped(404, "{\"message\":\"User not found for email someone@education.gov.uk\"}")));

    [Fact]
    public void IsMatch_False_ForUnrelated404()
        => Assert.False(TenantMembershipDenial.IsMatch(Untyped(404, "{\"message\":\"Template not found\"}")));

    [Fact]
    public void ErrorId_MatchesApiClientContract()
        => Assert.Equal("Exchange-403-NoTenantMembership", TenantMembershipDenial.ErrorId);
}
