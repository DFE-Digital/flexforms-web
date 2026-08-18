using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;

namespace GovUK.Dfe.FlexForms.Application.Dashboard;

/// <summary>
/// Dashboard row with a display status and optional custom column values.
/// Shared by the user dashboard and the admin applications index.
/// </summary>
public sealed class ApplicationWithCalculatedStatus
{
    public ApplicationDto Application { get; set; } = null!;

    public KeyValuePair<ApplicationStatus, string> CalculatedStatus { get; set; }

    public IReadOnlyDictionary<string, string> CustomColumnValues { get; set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public Guid ApplicationId => Application.ApplicationId;

    public string ApplicationReference => Application.ApplicationReference;

    public string TemplateName => Application.TemplateName;

    public DateTime DateCreated => Application.DateCreated;

    public DateTime? DateSubmitted => Application.DateSubmitted;
}
