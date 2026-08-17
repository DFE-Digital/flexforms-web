using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Application.Validation;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Mutable view-state bag for the User Manager Add admin page.
/// </summary>
public sealed class UserManagerAddWorkState
{
    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = "User";

    public List<Guid> SelectedTemplateIds { get; set; } = [];

    public IReadOnlyList<TemplateDto> AvailableTemplates { get; set; } = [];

    public IReadOnlyList<string> AssignableRoles { get; set; } = [];

    public bool IncludeTenantAdmin { get; set; }

    public IReadOnlyList<FormValidationError> Errors { get; set; } = [];
}
