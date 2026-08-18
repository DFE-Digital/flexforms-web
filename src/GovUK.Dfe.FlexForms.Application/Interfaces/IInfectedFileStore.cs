namespace GovUK.Dfe.FlexForms.Application.Interfaces;

/// <summary>
/// Application port for the malware-scan blacklist.
/// Implemented in Infrastructure against Redis.
/// </summary>
public interface IInfectedFileStore
{
    bool IsFileInfected(Guid fileId);

    bool IsFileNameInfected(string applicationId, string originalFileName);
}
