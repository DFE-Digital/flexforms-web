using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

/// <summary>
/// Removes malware-blacklisted uploads from file lists and JSON payloads.
/// </summary>
public interface IInfectedUploadFilter
{
    List<UploadDto> FilterList(IReadOnlyList<UploadDto>? files, string? applicationId);

    string FilterUploadDataJson(string? uploadDataJson, string? applicationId);
}
