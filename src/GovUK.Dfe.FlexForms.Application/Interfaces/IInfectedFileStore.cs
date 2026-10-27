namespace GovUK.Dfe.FlexForms.Application.Interfaces;

/// <summary>
/// Application port for the malware-scan blacklist.
/// Implemented in Infrastructure against Redis.
/// </summary>
public interface IInfectedFileStore
{
    bool IsFileInfected(Guid fileId);

    bool IsFileNameInfected(string applicationId, string originalFileName);

    /// <summary>
    /// Checks many uploaded files in one round trip, by file id and, when an application id is given, by original file name.
    /// </summary>
    /// <returns>The ids of the files that are blacklisted.</returns>
    IReadOnlySet<Guid> FindInfectedFiles(string? applicationId, IReadOnlyCollection<InfectedFileCandidate> files);
}

/// <summary>
/// An uploaded file to check against the malware-scan blacklist.
/// </summary>
public sealed record InfectedFileCandidate(Guid FileId, string? OriginalFileName);
