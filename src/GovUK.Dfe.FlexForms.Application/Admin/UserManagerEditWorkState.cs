using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Application.Validation;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Mutable view-state bag for the User Manager Edit admin page.
/// </summary>
public sealed class UserManagerEditWorkState
{
    public Guid UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string UserEmail { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public List<Guid> SelectedTemplateIds { get; set; } = [];

    public IReadOnlyList<TemplateDto> AvailableTemplates { get; set; } = [];

    public IReadOnlyList<string> AssignableRoles { get; set; } = [];

    public bool IncludeTenantAdmin { get; set; }

    public IReadOnlyList<FormValidationError> Errors { get; set; } = [];
}
