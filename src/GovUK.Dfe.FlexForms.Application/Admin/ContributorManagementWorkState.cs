using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Mutable view-state bag for Contributor Management.
/// </summary>
public sealed class ContributorManagementWorkState
{
    public const int EmailLookupPageSize = 10;

    public string ReferenceNumber { get; set; } = string.Empty;

    public bool LookupPerformed { get; set; }

    public string? ApplicationReference { get; set; }

    public Guid? ApplicationId { get; set; }

    public string? TemplateName { get; set; }

    public IReadOnlyList<UserDto> Contributors { get; set; } = [];

    public bool EmailLookupPerformed { get; set; }

    public string Email { get; set; } = string.Empty;

    public Guid? LookedUpUserId { get; set; }

    public string? LookedUpUserName { get; set; }

    public string? LookedUpUserEmail { get; set; }

    public IReadOnlyList<CreatedApplicationInviteSummary> CreatedApplications { get; set; } = [];

    public int CurrentPage { get; set; } = 1;

    public int PageSize { get; set; } = ContributorManagementWorkState.EmailLookupPageSize;

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    public bool HasError { get; set; }

    public string? ErrorMessage { get; set; }
}
