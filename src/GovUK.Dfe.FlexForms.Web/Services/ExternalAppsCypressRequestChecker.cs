using GovUK.Dfe.CoreLibs.Security.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Diagnostics.CodeAnalysis;

namespace GovUK.Dfe.FlexForms.Web.Services;

/// <summary>
/// Custom request checker for External Applications that validates Cypress test requests
/// using X-Cypress-Test and X-Cypress-Secret headers
/// </summary>
[ExcludeFromCodeCoverage]
public class ExternalAppsCypressRequestChecker(
    IHostEnvironment env,
    IConfiguration config,
    ILogger<ExternalAppsCypressRequestChecker> logger)
    : ICustomRequestChecker
{
    private const string CypressHeaderKey = "x-cypress-test";
    private const string CypressSecretHeaderKey = "x-cypress-secret";
    private const string ExpectedCypressValue = "true";
    private const string CacheKey = "__CypressRequestChecked";

    /// <summary>
    /// Validates if the current HTTP request is a valid Cypress test request
    /// </summary>
    /// <param name="httpContext">The HTTP context to validate</param>
    /// <returns>True if this is a valid Cypress request with correct headers and secret</returns>
    public bool IsValidRequest(HttpContext httpContext)
    {
        // Cache the result per request to prevent infinite recursion
        // If logging triggers any operation that requires authentication, it could call this method again
        if (httpContext.Items.TryGetValue(CacheKey, out var cachedResult))
        {
            return (bool)cachedResult!;
        }
        
        var result = ValidateRequestInternal(httpContext);
        httpContext.Items[CacheKey] = result;
        return result;
    }
    
    private bool ValidateRequestInternal(HttpContext httpContext)
    {
        // Only enable Cypress auth in GitHub Actions environment
        // This prevents the recursion issue from occurring in local/production environments
        var isGitHubActions = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
        if (!isGitHubActions)
        {
            try
            {
                logger.LogDebug("Cypress authentication disabled: Not running in GitHub Actions");
            }
            catch
            {
                // Silently ignore logging errors to prevent recursion
            }
            return false;
        }

        var path = httpContext.Request.Path;
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        
        // Check for Cypress header
        var cypressHeader = httpContext.Request.Headers[CypressHeaderKey].ToString();
        
        try
        {
            logger.LogDebug(
                "Checking Cypress header '{HeaderKey}' for path {Path}. Value: '{Value}' (expected: '{Expected}')",
                CypressHeaderKey, path, cypressHeader, ExpectedCypressValue);
        }
        catch
        {
            // Silently ignore logging errors to prevent recursion
        }
            
        if (!string.Equals(cypressHeader, ExpectedCypressValue, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                logger.LogDebug(
                    "Cypress header check failed for {Path} from {IP}. Header '{HeaderKey}' = '{Value}'",
                    path, ipAddress, CypressHeaderKey, cypressHeader);
            }
            catch
            {
                // Silently ignore logging errors to prevent recursion
            }
            return false;
        }

        // Only allow in Development, Staging or Test environments (NOT Production)
        if (!(env.IsDevelopment() || env.IsStaging() || env.IsEnvironment("Test")))
        {
            try
            {
                logger.LogWarning(
                    "Cypress authentication attempted in {Environment} environment from {IP} - rejected",
                    env.EnvironmentName,
                    httpContext.Connection.RemoteIpAddress);
            }
            catch
            {
                // Silently ignore logging errors to prevent recursion
            }
            return false;
        }

        // Check if Cypress toggle is allowed in configuration
        var allowCypressToggle = config.GetValue<bool>("CypressAuthentication:AllowToggle");
        if (!allowCypressToggle)
        {
            try
            {
                logger.LogWarning(
                    "Cypress authentication attempted but AllowToggle is disabled from {IP}",
                    httpContext.Connection.RemoteIpAddress);
            }
            catch
            {
                // Silently ignore logging errors to prevent recursion
            }
            return false;
        }

        // Validate secret
        var expectedSecret = config["CypressAuthentication:Secret"];
        var providedSecret = httpContext.Request.Headers[CypressSecretHeaderKey].ToString();

        if (string.IsNullOrWhiteSpace(expectedSecret) || string.IsNullOrWhiteSpace(providedSecret))
        {
            try
            {
                logger.LogWarning(
                    "Cypress authentication attempted with missing secret from {IP}",
                    httpContext.Connection.RemoteIpAddress);
            }
            catch
            {
                // Silently ignore logging errors to prevent recursion
            }
            return false;
        }

        var isValid = string.Equals(providedSecret, expectedSecret, StringComparison.Ordinal);

        if (isValid)
        {
            try
            {
                logger.LogInformation(
                    "Valid Cypress test request detected from {IP} for path {Path}",
                    httpContext.Connection.RemoteIpAddress,
                    httpContext.Request.Path);
            }
            catch
            {
                // Silently ignore logging errors to prevent recursion
            }
        }
        else
        {
            try
            {
                logger.LogWarning(
                    "Invalid Cypress secret provided from {IP} for path {Path}",
                    httpContext.Connection.RemoteIpAddress,
                    httpContext.Request.Path);
            }
            catch
            {
                // Silently ignore logging errors to prevent recursion
            }
        }

        return isValid;
    }
}

