using System.ComponentModel.DataAnnotations;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Validation;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Creates one-time codes that let a user set up a Test Authentication authenticator app without first receiving an
/// emailed password. The API enforces who may create a code for whom and audits every code.
/// </summary>
public interface ITestAuthAuthenticatorCodeAdmin
{
    Task<AdminPageOutcome> CreateAsync(TestAuthAuthenticatorCodeWorkState state, CancellationToken cancellationToken = default);
}

public sealed class TestAuthAuthenticatorCodeAdminService(
    IUsersClient usersClient,
    ILogger<TestAuthAuthenticatorCodeAdminService> logger) : ITestAuthAuthenticatorCodeAdmin
{
    private static readonly EmailAddressAttribute EmailFormat = new();

    public async Task<AdminPageOutcome> CreateAsync(
        TestAuthAuthenticatorCodeWorkState state,
        CancellationToken cancellationToken = default)
    {
        state.Code = null;
        state.ExpiresAtUtc = null;
        state.Email = state.Email.Trim();

        if (string.IsNullOrEmpty(state.Email))
            return Invalid(TestAuthAuthenticatorCodeMessages.EmailRequired);

        if (!EmailFormat.IsValid(state.Email))
            return Invalid(TestAuthAuthenticatorCodeMessages.EmailInvalid);

        try
        {
            var created = await usersClient.CreateTestAuthAuthenticatorInviteAsync(
                new CreateTestAuthAuthenticatorInviteRequest(state.Email),
                cancellationToken);

            state.Code = created.Code;
            state.ExpiresAtUtc = created.ExpiresAtUtc;
            return AdminPageOutcome.Stay(successMessage: TestAuthAuthenticatorCodeMessages.Created(state.Email));
        }
        catch (ExternalApplicationsException ex)
        {
            logger.LogError(ex, "Failed to create a test authentication authenticator setup code for {Email}", state.Email);
            return AdminPageOutcome.Stay(errorMessage: AdminApiErrorMapper.Format(ex, TestAuthAuthenticatorCodeMessages.CreateFailed));
        }
    }

    private static AdminPageOutcome Invalid(string message)
        => AdminPageOutcome.Stay(errors: [new FormValidationError(nameof(TestAuthAuthenticatorCodeWorkState.Email), message)]);
}
