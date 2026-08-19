using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// An application created by the looked-up user and the people they invited onto it.
/// </summary>
public sealed class CreatedApplicationInviteSummary
{
    public Guid ApplicationId { get; init; }

    public string ApplicationReference { get; init; } = string.Empty;

    public string? TemplateName { get; init; }

    public IReadOnlyList<UserDto> Invitees { get; init; } = [];
}
