using Microsoft.Extensions.Options;
using GovUK.Dfe.CoreLibs.Security.Configurations;
using GovUK.Dfe.CoreLibs.Security.Interfaces;
using System.Diagnostics.CodeAnalysis;

namespace GovUK.Dfe.FlexForms.Web.Services;

/// <summary>
/// Service to determine if test authentication should be enabled for the current request
/// </summary>
public interface ICypressAuthenticationService
{
    /// <summary>
    /// Checks if test authentication should be enabled for the current HTTP context
    /// </summary>
    /// <param name="httpContext">The current HTTP context</param>
    /// <returns>True if test authentication should be enabled</returns>
    bool ShouldEnableTestAuthentication(HttpContext? httpContext);
}

/// <summary>
/// Implementation of Cypress authentication service using the CoreLibs request checker pattern
/// </summary>
[ExcludeFromCodeCoverage]
public class CypressAuthenticationService(
    IOptions<TestAuthenticationOptions> testAuthOptions,
    [FromKeyedServices("cypress")] ICustomRequestChecker requestChecker)
    : ICypressAuthenticationService
{
    private readonly TestAuthenticationOptions _testAuthOptions = testAuthOptions.Value;

    public bool ShouldEnableTestAuthentication(HttpContext? httpContext)
    {
        // First check if test authentication is already enabled globally
        if (_testAuthOptions.Enabled)
        {
            return true;
        }

        // Use the CoreLibs request checker to validate if this is a valid Cypress request
        if (httpContext != null && requestChecker.IsValidRequest(httpContext))
        {
            return true;
        }

        return false;
    }
}

