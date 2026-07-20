using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Diagnostics.CodeAnalysis;

namespace GovUK.Dfe.FlexForms.Web.Services;

public interface ITestAuthenticationService
{
    /// <summary>
    /// Authenticates a user with the provided email address and generates a test token
    /// </summary>
    /// <param name="email">The email address to authenticate</param>
    /// <param name="httpContext">The current HTTP context</param>
    /// <returns>A task representing the authentication operation</returns>
    Task<TestAuthenticationResult> AuthenticateAsync(string email, HttpContext httpContext);
    
    /// <summary>
    /// Signs out the current test authentication session
    /// </summary>
    /// <param name="httpContext">The current HTTP context</param>
    /// <returns>A task representing the sign out operation</returns>
    Task SignOutAsync(HttpContext httpContext);
}

[ExcludeFromCodeCoverage]
public class TestAuthenticationResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public string? RedirectUrl { get; init; }
    
    public static TestAuthenticationResult Success(string redirectUrl) => new()
    {
        IsSuccess = true,
        RedirectUrl = redirectUrl
    };
    
    public static TestAuthenticationResult Failure(string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorMessage = errorMessage
    };
} 