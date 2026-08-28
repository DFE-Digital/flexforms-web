using GovUK.Dfe.CoreLibs.Http.Models;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;

namespace GovUK.Dfe.FlexForms.Application.Tests.Admin;

public class AdminApiErrorMapperTests
{
    [Fact]
    public void Format_ShouldReturnDetails_WhenValidationMessageIsGenericHeader()
    {
        var ex = new ExternalApplicationsException<ExceptionResponse>(
            "Validation failed",
            400,
            "body",
            new Dictionary<string, IEnumerable<string>>(),
            new ExceptionResponse
            {
                Message = "Validation failed. Please check the following errors:",
                Details = "Email: 'notanemail' is not a valid email address."
            },
            null);

        var message = AdminApiErrorMapper.Format(ex, "fallback");

        Assert.Equal("Email: 'notanemail' is not a valid email address.", message);
    }

    [Fact]
    public void Format_ShouldReturnMessage_WhenDetailsAreAbsent()
    {
        var ex = new ExternalApplicationsException<ExceptionResponse>(
            "Forbidden",
            403,
            "body",
            new Dictionary<string, IEnumerable<string>>(),
            new ExceptionResponse
            {
                Message = "Only administrators can perform this action."
            },
            null);

        var message = AdminApiErrorMapper.Format(ex, "fallback");

        Assert.Equal("Only administrators can perform this action.", message);
    }
}
